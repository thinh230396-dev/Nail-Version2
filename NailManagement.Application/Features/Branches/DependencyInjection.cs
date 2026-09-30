using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Branches.UseCases;

namespace NailManagement.Application.Features.Branches;

public static class BranchesFeatureRegistration
{
    public static IServiceCollection AddBranchesFeature(this IServiceCollection services)
    {
        services.AddScoped<BranchQuotaGuard>();
        services.AddScoped<ListBranchesUseCase>();
        services.AddScoped<CreateBranchUseCase>();
        services.AddScoped<UpdateBranchUseCase>();
        services.AddScoped<ChangeBranchStatusUseCase>();

        return services;
    }
}
