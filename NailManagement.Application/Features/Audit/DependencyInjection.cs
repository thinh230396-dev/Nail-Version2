using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Audit.UseCases;

namespace NailManagement.Application.Features.Audit;

public static class AuditFeatureRegistration
{
    public static IServiceCollection AddAuditFeature(this IServiceCollection services)
    {
        services.AddScoped<ListAuditLogsUseCase>();

        return services;
    }
}
