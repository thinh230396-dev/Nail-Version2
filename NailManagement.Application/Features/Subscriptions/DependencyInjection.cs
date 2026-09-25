using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Subscriptions.UseCases;

namespace NailManagement.Application.Features.Subscriptions;

public static class SubscriptionsFeatureRegistration
{
    public static IServiceCollection AddSubscriptionsFeature(this IServiceCollection services)
    {
        services.AddScoped<ListSubscriptionInvoicesUseCase>();

        return services;
    }
}
