using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Diagnostics;
using System.Text.Json;

namespace meet365_API.Middleware
{
    public class ExceptionMiddleware : IMiddleware
    {
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionMiddleware(ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                var requestId = Activity.Current?.Id ?? context.TraceIdentifier;
                _logger.LogError(ex, "Unhandled exception {RequestId} at {Path}", requestId, context.Request.Path);

                // Build a rich model once
                var model = new ErrorViewModel
                {
                    RequestId = requestId,
                    ErrorMessage = ex.Message,
                    Path = context.Request.Path,
                    QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                    Endpoint = context.GetEndpoint()?.DisplayName,
                    RouteValues = context.Request.RouteValues.ToDictionary(k => k.Key, v => v.Value?.ToString()),
                    ExceptionType = ex.GetType().FullName,
                    Source = ex.Source,
                    TargetSite = ex.TargetSite?.ToString(),
                    StackTrace = ex.StackTrace,
                    User = context.User?.Identity?.IsAuthenticated == true ? context.User.Identity!.Name : null,
                    Utc = DateTimeOffset.UtcNow
                };

                if (IsApiRequest(context))
                {
                    // ---- API response (JSON) ----
                    if (!context.Response.HasStarted)
                    {
                        context.Response.Clear();
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        context.Response.ContentType = "application/json";

                        var payload = new
                        {
                            requestId,
                            title = "An unexpected error occurred.",
                            status = 500,
                            // expose details only in Development
                            detail = _env.IsDevelopment() ? ex.Message : null,
                            exceptionType = _env.IsDevelopment() ? ex.GetType().FullName : null,
                            stackTrace = _env.IsDevelopment() ? ex.StackTrace : null,
                            path = model.Path.ToString()
                        };

                        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            WriteIndented = _env.IsDevelopment()
                        });

                        await context.Response.WriteAsync(json);
                    }
                    return;
                }
                else
                {
                    // ---- MVC response (Razor view) ----
                    if (!context.Response.HasStarted)
                    {
                        context.Response.Clear();
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                        // Build a full ActionContext: HttpContext + RouteData + ActionDescriptor
                        var routeData = context.GetRouteData() ?? new RouteData();
                        var actionContext = new ActionContext(
                            httpContext: context,
                            routeData: routeData,
                            actionDescriptor: new ActionDescriptor()
                        );

                        var viewResult = new ViewResult
                        {
                            ViewName = "~/Views/Shared/Error.cshtml",
                            ViewData = new ViewDataDictionary(
                                new EmptyModelMetadataProvider(),
                                new ModelStateDictionary())
                            { Model = model }
                        };

                        var executor = context.RequestServices.GetRequiredService<IActionResultExecutor<ViewResult>>();
                        await executor.ExecuteAsync(actionContext, viewResult);
                    }
                    return;
                }
            }
        }

        private static bool IsApiRequest(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var accept = context.Request.Headers["Accept"].ToString();

            return path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
                   accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
