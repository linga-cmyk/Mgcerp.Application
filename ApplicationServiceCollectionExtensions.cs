using Mgcerp.Application.Common;
using Mgcerp.Application.Services;
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
            //services.AddScoped<IDapperHelper, DapperHelper>();
            services.AddScoped<IApplicationCommonServices, ApplicationCommonServices>();
            services.AddScoped<IEInvoiceLogService, EInvoiceLogService>();
            return services;
        }
    }
}

