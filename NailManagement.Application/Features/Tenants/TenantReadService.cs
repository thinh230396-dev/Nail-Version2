using NailManagement.Application.Common;
using NailManagement.Domain.Auth;
using NailManagement.Domain.Platform.Tenants;

namespace NailManagement.Application.Features.Tenants;

/// <summary>
/// Dựng <see cref="TenantDetailDto"/> từ một tiệm: đếm mức sử dụng, tra chủ tiệm, rồi ghép
/// lại qua mapper.
/// <para>
/// Tách ra vì <b>sáu</b> use case của module tiệm đều kết thúc bằng đúng ba bước này —
/// liệt kê, xem chi tiết, tạo, sửa, gia hạn, đổi trạng thái. Chép ba bước đó vào từng use
/// case là mở đường cho một use case nào đó quên đếm, rồi trả về số 0 như thể tiệm không có
/// nhân viên nào.
/// </para>
/// <para>
/// Không phải use case: nó không đại diện cho một tình huống sử dụng nào và không có
/// <c>ExecuteAsync</c>. Đây là một dịch vụ nội bộ của tầng Application, và nó chỉ đọc.
/// </para>
/// </summary>
public sealed class TenantReadService(
    ITenantRepository tenants,
    IUserTenantRepository userTenants,
    TenantPlanReader plans)
{
    public async Task<IReadOnlyList<TenantDetailDto>> DescribeManyAsync(
        IReadOnlyList<Tenant> source, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (source.Count == 0) return [];

        var ids = source.Select(tenant => tenant.Id).ToArray();
        var withPlans = await plans.AttachAsync(source, cancellationToken);
        var usage = await tenants.ReadUsageAsync(ids, cancellationToken);
        var owners = await userTenants.ListOwnersAsync(ids, cancellationToken);

        return [.. withPlans.Select(plan => Describe(plan, usage, owners, now))];
    }

    public async Task<TenantDetailDto> DescribeAsync(
        Tenant tenant, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        string[] ids = [tenant.Id];
        var plan = await plans.AttachAsync(tenant, cancellationToken);
        var usage = await tenants.ReadUsageAsync(ids, cancellationToken);
        var owners = await userTenants.ListOwnersAsync(ids, cancellationToken);

        return Describe(plan, usage, owners, now);
    }

    private static TenantDetailDto Describe(
        TenantPlan plan,
        IReadOnlyDictionary<string, TenantUsage> usage,
        IReadOnlyDictionary<string, IReadOnlyList<AppUser>> owners,
        DateTimeOffset now)
    {
        var tenant = plan.Tenant;
        var tenantOwners = owners.TryGetValue(tenant.Id, out var list) ? list : [];

        return TenantMapper.ToDetail(
            tenant,
            plan.Package,
            usage.TryGetValue(tenant.Id, out var counts) ? counts : null,
            [.. tenantOwners.Select(TenantMapper.ToOwner)],
            now);
    }
}
