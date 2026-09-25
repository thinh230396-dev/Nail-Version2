using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Tenants.UseCases;

namespace NailManagement.Application.Features.Tenants;

public static class TenantsFeatureRegistration
{
    public static IServiceCollection AddTenantsFeature(this IServiceCollection services)
    {
        services.AddScoped<TenantReadService>();
        services.AddScoped<ListTenantsUseCase>();
        services.AddScoped<GetTenantUseCase>();
        services.AddScoped<GetMyTenantUseCase>();
        services.AddScoped<CreateTenantUseCase>();
        services.AddScoped<UpdateTenantUseCase>();
        services.AddScoped<RenewTenantUseCase>();
        services.AddScoped<ChangeTenantStatusUseCase>();
        services.AddScoped<DeleteTenantUseCase>();

        return services;
    }
}
