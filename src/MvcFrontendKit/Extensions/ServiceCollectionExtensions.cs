using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MvcFrontendKit.Services;

namespace MvcFrontendKit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMvcFrontendKit(this IServiceCollection services)
    {
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.TryAddSingleton<IFrontendConfigProvider, FrontendConfigProvider>();

        services.TryAddSingleton<IFrontendManifestProvider, FrontendManifestProvider>();

        services.TryAddScoped<IFrontendComponentRegistry, FrontendComponentRegistry>();

        return services;
    }
}
