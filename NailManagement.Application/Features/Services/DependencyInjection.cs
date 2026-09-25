using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Services.UseCases;

namespace NailManagement.Application.Features.Services;

public static class ServicesFeatureRegistration
{
    public static IServiceCollection AddServicesFeature(this IServiceCollection services)
    {
        services.AddScoped<ListServicesUseCase>();
        services.AddScoped<CreateServiceUseCase>();
        services.AddScoped<UpdateServiceUseCase>();
        services.AddScoped<ChangeServiceStatusUseCase>();

        return services;
    }
}
