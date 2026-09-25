using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Reports.UseCases;

namespace NailManagement.Application.Features.Reports;

public static class ReportsFeatureRegistration
{
    public static IServiceCollection AddReportsFeature(this IServiceCollection services)
    {
        services.AddScoped<GetRevenueReportUseCase>();

        return services;
    }
}
