using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Accounts.UseCases;

namespace NailManagement.Application.Features.Accounts;

public static class AccountsFeatureRegistration
{
    public static IServiceCollection AddAccountsFeature(this IServiceCollection services)
    {
        services.AddScoped<ListTenantAdminAccountsUseCase>();
        services.AddScoped<ChangeAccountStatusUseCase>();

        return services;
    }
}
