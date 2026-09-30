using NailManagement.Application.Common.Exceptions;
using NailManagement.Domain.Platform.Packages;
using NailManagement.Domain.Platform.Tenants;

namespace NailManagement.Application.Common;

/// <summary>Một tiệm cùng gói đăng ký nó đang dùng.</summary>
public sealed record TenantPlan(Tenant Tenant, Package Package);

/// <summary>
/// Đọc tiệm kèm gói đăng ký — hai aggregate khác nhau, nối lại ở tầng ứng dụng.
/// <para>
/// <c>Tenant</c> chỉ giữ <c>PackageId</c>, không giữ thuộc tính điều hướng sang <c>Package</c>:
/// giữa hai aggregate chỉ tham chiếu bằng mã. Nhờ vậy không đoạn mã nào sửa được một gói "tiện
/// tay" qua một tiệm, và không truy vấn nào lặng lẽ trả về tiệm thiếu gói vì quên
/// <c>Include</c>. Cái giá là một phép đọc thứ hai; bảng gói chỉ có vài dòng nên cái giá ấy
/// không đáng kể.
/// </para>
/// </summary>
public sealed class TenantPlanReader(ITenantRepository tenants, IPackageRepository packages)
{
    /// <summary>Tiệm kèm gói, hoặc <see cref="NotFoundException"/> nếu không có tiệm này.</summary>
    public async Task<TenantPlan> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tiệm đang làm việc.");

        return await AttachAsync(tenant, cancellationToken);
    }

    /// <summary>Gắn gói cho một tiệm đã đọc sẵn.</summary>
    public async Task<TenantPlan> AttachAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        var package = await packages.FindByIdAsync(tenant.PackageId, cancellationToken)
            ?? throw MissingPackage(tenant);

        return new TenantPlan(tenant, package);
    }

    /// <summary>
    /// Gắn gói cho cả một danh sách tiệm bằng <b>một</b> phép đọc bảng gói, thay vì một phép
    /// đọc cho mỗi tiệm.
    /// </summary>
    public async Task<IReadOnlyList<TenantPlan>> AttachAsync(
        IReadOnlyCollection<Tenant> found, CancellationToken cancellationToken = default)
    {
        if (found.Count == 0) return [];

        var byId = (await packages.ListAsync(cancellationToken))
            .ToDictionary(package => package.Id, StringComparer.Ordinal);

        return [.. found.Select(tenant => new TenantPlan(
            tenant,
            byId.TryGetValue(tenant.PackageId, out var package) ? package : throw MissingPackage(tenant)))];
    }

    // Khóa ngoại Tenants.PackageId bảo đảm gói luôn tồn tại, nên tới được đây là dữ liệu hỏng
    // hoặc lỗi lập trình — không phải lỗi người dùng gây ra.
    private static InvalidOperationException MissingPackage(Tenant tenant)
        => new($"Tiệm {tenant.Id} trỏ tới gói {tenant.PackageId} nhưng gói đó không tồn tại.");
}
