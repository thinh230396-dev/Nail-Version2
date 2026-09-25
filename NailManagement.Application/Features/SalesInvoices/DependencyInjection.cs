using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.SalesInvoices.UseCases;

namespace NailManagement.Application.Features.SalesInvoices;

public static class SalesInvoicesFeatureRegistration
{
    public static IServiceCollection AddSalesInvoicesFeature(this IServiceCollection services)
    {
        services.AddScoped<SalesInvoiceLineBuilder>();
        services.AddScoped<ListSalesInvoicesUseCase>();
        services.AddScoped<GetSalesInvoiceUseCase>();
        services.AddScoped<CreateSalesInvoiceUseCase>();
        services.AddScoped<UpdateSalesInvoiceUseCase>();
        services.AddScoped<ChangeSalesInvoiceStatusUseCase>();
        services.AddScoped<RecordPaymentUseCase>();
        services.AddScoped<IssueRefundUseCase>();

        return services;
    }
}
