using D365_Shared_Library.Connection;
using D365_Shared_Library.Repository;
using Kiddopay;
using Microsoft.Xrm.Sdk;

namespace Kiddopay.Extensions
{
    public static class CRMConnectionExtension
    {
        public static IServiceCollection ServiceAndBaseRepository(
            this IServiceCollection services, IConfiguration config, string aspNetEnvironmentName)
        {
            // Driven by the ASP.NET Core hosting environment (ASPNETCORE_ENVIRONMENT / the
            // --environment flag) instead of a compiled constant, so a Production build can
            // no longer silently connect to the Development Dataverse environment.
            var crmEnvironmentTarget = aspNetEnvironmentName switch
            {
                "Production" => EnvironmentsNames.Production,
                "Staging"    => EnvironmentsNames.SandBox,
                _            => EnvironmentsNames.Development,
            };

            var serviceURL = config[$"Environments:{crmEnvironmentTarget}:ServiceURL"] ?? "";
            var clientId = config[$"Environments:{crmEnvironmentTarget}:ClientId"] ?? "";
            var clientSecret =config[$"Environments:{crmEnvironmentTarget}:ClientSecret"] ?? "";

            var service = ServiceManager.GetService(serviceURL, clientId, clientSecret);

            services.AddSingleton<IOrganizationService>(service!);

            services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
            return services;
        }
    }

    class EnvironmentsNames
    {
        public const string Development = "Development";
        public const string SandBox = "SandBox";
        public const string Production = "Production";
    }
}
