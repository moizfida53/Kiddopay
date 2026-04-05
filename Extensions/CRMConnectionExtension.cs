using D365_Shared_Library.Connection;
using D365_Shared_Library.Repository;
using Kiddopay;
using Microsoft.Xrm.Sdk;

namespace Kiddopay.Extensions
{
    public static class CRMConnectionExtension
    {
        public static readonly String CRMEnvironmentTarget = EnvironmentsNames.Development;

        public static IServiceCollection ServiceAndBaseRepository(this IServiceCollection services, IConfiguration config)
        {
            var serviceURL = config[$"Environments:{CRMEnvironmentTarget}:ServiceURL"] ?? "";
            var clientId = config[$"Environments:{CRMEnvironmentTarget}:ClientId"] ?? "";
            var clientSecret =config[$"Environments:{CRMEnvironmentTarget}:ClientSecret"] ?? "";

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
