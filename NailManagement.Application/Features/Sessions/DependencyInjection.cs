using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Sessions.UseCases;

namespace NailManagement.Application.Features.Sessions;

public static class SessionsFeatureRegistration
{
    public static IServiceCollection AddSessionsFeature(this IServiceCollection services)
    {
        services.AddScoped<ListSessionsUseCase>();
        services.AddScoped<RevokeSessionUseCase>();

        return services;
    }
}
