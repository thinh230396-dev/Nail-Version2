using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Accounts;
using NailManagement.Application.Features.Appointments;
using NailManagement.Application.Features.Audit;
using NailManagement.Application.Features.Auth;
using NailManagement.Application.Features.Branches;
using NailManagement.Application.Features.Customers;
using NailManagement.Application.Features.Packages;
using NailManagement.Application.Features.Reports;
using NailManagement.Application.Features.SalesInvoices;
using NailManagement.Application.Features.Services;
using NailManagement.Application.Features.Sessions;
using NailManagement.Application.Features.Staff;
using NailManagement.Application.Features.Subscriptions;
using NailManagement.Application.Features.Tenants;

namespace NailManagement.Application;

/// <summary>Đăng ký các feature của Application; hạ tầng được đăng ký riêng.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAccountsFeature();
        services.AddAppointmentsFeature();
        services.AddAuditFeature();
        services.AddAuthFeature();
        services.AddBranchesFeature();
        services.AddCustomersFeature();
        services.AddPackagesFeature();
        services.AddReportsFeature();
        services.AddSalesInvoicesFeature();
        services.AddServicesFeature();
        services.AddSessionsFeature();
        services.AddStaffFeature();
        services.AddSubscriptionsFeature();
        services.AddTenantsFeature();

        return services;
    }
}
