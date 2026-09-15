using D365_Shared_Library.Repository;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Kiddopay.BLL.Interfaces;
using Kiddopay.BLL.Services;
using Kiddopay.Extensions;
using KiddoPay.BLL.Interfaces;
using KiddoPay.BLL.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.ServiceAndBaseRepository(builder.Configuration, builder.Environment.EnvironmentName);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IinventoryService, InventoryService>();
builder.Services.AddScoped<IPreOrderService, PreOrderService>();
builder.Services.AddScoped<ICashierService, CashierService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IDeviceTokenService, DeviceTokenService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IParentService, ParentService>();
builder.Services.AddScoped<ITokenService, TokenService>();
// PLACEHOLDER -- logs OTP emails instead of sending them. Swap for a real
// provider (SendGrid, Azure Communication Services, SMTP, etc.) once one is
// chosen; nothing else needs to change, everything depends on IEmailService.
builder.Services.AddScoped<IEmailService, DevLoggingEmailService>();

builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));

// Firebase Admin SDK (push notifications) -- initialized once at startup.
// Two ways to supply the credential, checked in this order:
//   1. Firebase:ServiceAccountJson -- the *contents* of the key file, as a
//      single config value. This is what lets the key live in Azure App
//      Service's "Application settings" (or Key Vault) instead of a file on
//      disk, which is the recommended way to run this in production -- see
//      the Azure App Service hosting guide.
//   2. Firebase:ServiceAccountKeyPath -- a file on disk (defaults to
//      firebase-service-account.json next to the app). This is what local
//      development still uses, unchanged.
// Defensive either way: if neither is present/valid, skip initialization
// instead of crashing the whole app. NotificationService checks
// FirebaseApp.DefaultInstance itself and no-ops when it's null.
var firebaseJson = builder.Configuration["Firebase:ServiceAccountJson"];
var firebaseKeyPath = builder.Configuration["Firebase:ServiceAccountKeyPath"];

try
{
    GoogleCredential? credential = null;

    if (!string.IsNullOrWhiteSpace(firebaseJson))
    {
        credential = GoogleCredential.FromJson(firebaseJson);
        Console.WriteLine("[Startup] Firebase credential loaded from Firebase:ServiceAccountJson.");
    }
    else if (!string.IsNullOrWhiteSpace(firebaseKeyPath) && File.Exists(firebaseKeyPath))
    {
        credential = GoogleCredential.FromFile(firebaseKeyPath);
        Console.WriteLine($"[Startup] Firebase credential loaded from file '{firebaseKeyPath}'.");
    }

    if (credential != null)
    {
        FirebaseApp.Create(new AppOptions { Credential = credential });
        Console.WriteLine("[Startup] Firebase initialized -- push notifications are enabled.");
    }
    else
    {
        Console.WriteLine("[Startup] No Firebase credential configured (Firebase:ServiceAccountJson or Firebase:ServiceAccountKeyPath) -- push notifications are disabled until one is provided.");
    }
}
catch (Exception ex)
{
    // A credential value/file was present but couldn't be parsed (corrupted
    // download, truncated app setting, wrong file, etc.) -- log and keep
    // starting up rather than taking the whole app down over a bad
    // credential. FirebaseApp.DefaultInstance stays null, so
    // NotificationService still no-ops safely, exactly as if none were set.
    Console.WriteLine($"[Startup] Firebase credential could not be loaded ({ex.Message}) -- push notifications are disabled until it's fixed.");
}

// Add JWT Bearer Authentication -- this is the scheme cashiers/staff use
// (Microsoft/Azure AD SSO), and stays the *default* scheme so every existing
// [Authorize] attribute keeps working unchanged.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
.EnableTokenAcquisitionToCallDownstreamApi()
.AddInMemoryTokenCaches();

// Second, independent JWT scheme for the parent mobile app -- parents don't
// use Microsoft/Azure AD login, they register/log in with email+password+OTP
// entirely through ParentsController, which issues its own token (see
// TokenService). Calling AddAuthentication() again with no argument is the
// supported way to register an *additional* scheme without touching the
// default set above -- controllers opt into this one explicitly with
// [Authorize(AuthenticationSchemes = "ParentScheme")] (see
// DeviceTokensController for an example).
var parentAuthSection = builder.Configuration.GetSection("ParentAuth");
builder.Services.AddAuthentication()
    .AddJwtBearer("ParentScheme", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = parentAuthSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = parentAuthSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(parentAuthSection["JwtSigningKey"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy.WithOrigins("https://localhost:44440")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()); // optional
});

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAngular");
    app.UseSwagger();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller}/{action=Index}/{id?}");

app.MapFallbackToFile("index.html");

app.Run();