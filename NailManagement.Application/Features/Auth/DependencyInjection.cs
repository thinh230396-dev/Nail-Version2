using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Auth.UseCases;

namespace NailManagement.Application.Features.Auth;

public static class AuthFeatureRegistration
{
    public static IServiceCollection AddAuthFeature(this IServiceCollection services)
    {
        services.AddScoped<LoginUseCase>();
        services.AddScoped<GetCurrentAccountUseCase>();
        services.AddScoped<LogoutUseCase>();
        services.AddScoped<ListMyTenantsUseCase>();
        services.AddScoped<SelectActiveTenantUseCase>();

        return services;
    }
}
