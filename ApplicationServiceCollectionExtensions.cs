using Mgcerp.Application.Common;
using Mgcerp.Infrastructure.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mgcerp.Application
{
    
    public static class ApplicationServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddScoped<IDapperHelper, DapperHelper>();
            services.AddScoped<IApplicationCommonServices, ApplicationCommonServices>();

            return services;
        }
    }
}

