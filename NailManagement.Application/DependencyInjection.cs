using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.UseCases.Audit;
using NailManagement.Application.UseCases.Auth;
using NailManagement.Application.UseCases.Branches;
using NailManagement.Application.UseCases.Packages;
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
        services.AddScoped<CreateTenantUseCase>();
        services.AddScoped<UpdateTenantUseCase>();
        services.AddScoped<RenewTenantUseCase>();
        services.AddScoped<ChangeTenantStatusUseCase>();
        services.AddScoped<DeleteTenantUseCase>();
        services.AddScoped<ListPackagesUseCase>();

        // ── Nghiệp vụ tiệm: chi nhánh ─────────────────────────────────────────
        services.AddScoped<ListBranchesUseCase>();
        services.AddScoped<CreateBranchUseCase>();
        services.AddScoped<UpdateBranchUseCase>();
        services.AddScoped<ChangeBranchStatusUseCase>();

        // ── Hệ thống ──────────────────────────────────────────────────────────
        services.AddScoped<ListAuditLogsUseCase>();

        return services;
    }
}
