using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Staff.UseCases;

namespace NailManagement.Application.Features.Staff;

public static class StaffFeatureRegistration
{
    public static IServiceCollection AddStaffFeature(this IServiceCollection services)
    {
        services.AddScoped<StaffQuotaGuard>();
        services.AddScoped<ListStaffUseCase>();
        services.AddScoped<CreateStaffUseCase>();
        services.AddScoped<UpdateStaffUseCase>();
        services.AddScoped<ChangeStaffStatusUseCase>();
        services.AddScoped<GrantStaffAccountUseCase>();

        return services;
    }
}
