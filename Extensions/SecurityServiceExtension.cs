

namespace Kiddopay.Extensions
{
    public static class SecurityServiceExtension
    {
        public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration config)
        {
            // Add CORS policy
            services.AddCors(options =>
            {
                options.AddPolicy("AngularOnly", builder =>
                {
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetPreflightMaxAge(TimeSpan.FromMinutes(10)); // Works in NET 8
                });
            });


            return services;
        }
    }

}
