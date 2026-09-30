using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Packages.UseCases;

namespace NailManagement.Application.Features.Packages;

public static class PackagesFeatureRegistration
{
    public static IServiceCollection AddPackagesFeature(this IServiceCollection services)
    {
        services.AddScoped<ListPackagesUseCase>();

        return services;
    }
}
