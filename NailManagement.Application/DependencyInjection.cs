using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.UseCases.Accounts;
using NailManagement.Application.UseCases.Appointments;
using NailManagement.Application.UseCases.Audit;
using NailManagement.Application.UseCases.Auth;
using NailManagement.Application.UseCases.Branches;
using NailManagement.Application.UseCases.Customers;
using NailManagement.Application.UseCases.Packages;
using NailManagement.Application.UseCases.Services;
using NailManagement.Application.UseCases.Staff;
using NailManagement.Application.UseCases.Tenants;

namespace NailManagement.Application;

/// <summary>
/// Đăng ký các use case.
/// <para>
/// Tầng Application chỉ khai báo use case; nó không đăng ký bất kỳ bản cài đặt hạ tầng nào —
/// đó là việc của <c>NailManagement.Infrastructure.DependencyInjection</c>.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // ── Xác thực và phiên ─────────────────────────────────────────────────
        services.AddScoped<LoginUseCase>();
        services.AddScoped<GetCurrentAccountUseCase>();
        services.AddScoped<LogoutUseCase>();
        services.AddScoped<ListMyTenantsUseCase>();
        services.AddScoped<SelectActiveTenantUseCase>();

        // ── Tầng nền tảng: tiệm và bảng giá ───────────────────────────────────
        services.AddScoped<TenantReadService>();
        services.AddScoped<ListTenantsUseCase>();
        services.AddScoped<GetTenantUseCase>();
        services.AddScoped<GetMyTenantUseCase>();
        services.AddScoped<CreateTenantUseCase>();
        services.AddScoped<UpdateTenantUseCase>();
        services.AddScoped<RenewTenantUseCase>();
        services.AddScoped<ChangeTenantStatusUseCase>();
        services.AddScoped<DeleteTenantUseCase>();
        services.AddScoped<ListPackagesUseCase>();
        services.AddScoped<ListTenantAdminAccountsUseCase>();

        // ── Nghiệp vụ tiệm: chi nhánh ─────────────────────────────────────────
        services.AddScoped<ListBranchesUseCase>();
        services.AddScoped<CreateBranchUseCase>();
        services.AddScoped<UpdateBranchUseCase>();
        services.AddScoped<ChangeBranchStatusUseCase>();

        // ── Nghiệp vụ tiệm: dịch vụ ───────────────────────────────────────────
        services.AddScoped<ListServicesUseCase>();
        services.AddScoped<CreateServiceUseCase>();
        services.AddScoped<UpdateServiceUseCase>();
        services.AddScoped<ChangeServiceStatusUseCase>();

        // ── Nghiệp vụ tiệm: khách hàng ───────────────────────────────────────
        services.AddScoped<ListCustomersUseCase>();
        services.AddScoped<GetCustomerUseCase>();
        services.AddScoped<CreateCustomerUseCase>();
        services.AddScoped<UpdateCustomerUseCase>();
        services.AddScoped<ChangeCustomerStatusUseCase>();

        // ── Nghiệp vụ tiệm: nhân viên ─────────────────────────────────────────
        // StaffQuotaGuard không phải use case; nó là phép đếm hạn mức dùng chung cho hai
        // đường vào (thêm mới và nhận lại người cũ) nên đăng ký cạnh chúng.
        services.AddScoped<StaffQuotaGuard>();
        services.AddScoped<ListStaffUseCase>();
        services.AddScoped<CreateStaffUseCase>();
        services.AddScoped<UpdateStaffUseCase>();
        services.AddScoped<ChangeStaffStatusUseCase>();
        services.AddScoped<GrantStaffAccountUseCase>();

        // ── Nghiệp vụ tiệm: lịch hẹn ──────────────────────────────────────────
        // AppointmentBookingGuard không phải use case; nó là bộ kiểm tra dùng chung cho cả ba
        // đường ghi lịch hẹn — đặt mới, sửa trọn, dời giờ — nên đăng ký cạnh chúng.
        services.AddScoped<AppointmentBookingGuard>();
        services.AddScoped<ListAppointmentsUseCase>();
        services.AddScoped<GetAppointmentUseCase>();
        services.AddScoped<CreateAppointmentUseCase>();
        services.AddScoped<UpdateAppointmentUseCase>();
        services.AddScoped<RescheduleAppointmentUseCase>();
        services.AddScoped<ChangeAppointmentStatusUseCase>();

        // ── Hệ thống ──────────────────────────────────────────────────────────
        services.AddScoped<ListAuditLogsUseCase>();

        return services;
    }
}
