using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Entities;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Tenants;

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
    IUserTenantRepository userTenants)
{
    public async Task<IReadOnlyList<TenantDetailDto>> DescribeManyAsync(
        IReadOnlyList<Tenant> source, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (source.Count == 0) return [];

        var ids = source.Select(tenant => tenant.Id).ToArray();
        var usage = await tenants.ReadUsageAsync(ids, cancellationToken);
        var owners = await userTenants.ListOwnersAsync(ids, cancellationToken);

        return [.. source.Select(tenant => Describe(tenant, usage, owners, now))];
    }

    public async Task<TenantDetailDto> DescribeAsync(
        Tenant tenant, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        string[] ids = [tenant.Id];
        var usage = await tenants.ReadUsageAsync(ids, cancellationToken);
        var owners = await userTenants.ListOwnersAsync(ids, cancellationToken);

        return Describe(tenant, usage, owners, now);
    }

    private static TenantDetailDto Describe(
        Tenant tenant,
        IReadOnlyDictionary<string, TenantUsage> usage,
        IReadOnlyDictionary<string, IReadOnlyList<AppUser>> owners,
        DateTimeOffset now)
    {
        // Gói bắt buộc phải được nạp kèm — hợp đồng của ITenantRepository nói rõ như vậy.
        // Thiếu nó là lỗi lập trình ở tầng lưu trữ, không phải lỗi nghiệp vụ, nên ném ngoại
        // lệ thường để nó hiện trong log máy chủ thay vì lặng lẽ trả ra một tiệm không gói.
        if (tenant.Package is null)
            throw new InvalidOperationException(
                $"Tiệm {tenant.Id} không đọc được gói đăng ký. Kho dữ liệu phải trả về tiệm kèm gói.");

        var tenantOwners = owners.TryGetValue(tenant.Id, out var list) ? list : [];

        return TenantMapper.ToDetail(
            tenant,
            tenant.Package,
            usage.TryGetValue(tenant.Id, out var counts) ? counts : null,
            [.. tenantOwners.Select(TenantMapper.ToOwner)],
            now);
    }
}
