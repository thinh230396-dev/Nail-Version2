using NailManagement.Application.Abstractions;
using NailManagement.Application.Features.Tenants;
using NailManagement.Domain.Auth;
using NailManagement.Domain.Platform.Tenants;

namespace NailManagement.Application.Features.Auth.UseCases;

/// <summary>
/// Danh sách tiệm mà tài khoản được giao quản lý — nguồn dữ liệu của màn chọn tiệm.
/// <para>
/// BR-AUTH-023 — một tài khoản chủ tiệm quản lý được nhiều tiệm, và bảng nối
/// <c>UserTenants</c> là nơi duy nhất trả lời "những tiệm nào". Không suy ra từ dữ liệu
/// khác, không đoán theo email.
/// </para>
/// <para>
/// Danh sách trả về kèm trạng thái hiển thị tính lúc đọc (BR-TENANT-002), để người dùng
/// nhìn thấy ngay tiệm nào đã quá hạn <b>trước khi</b> chọn vào rồi mới phát hiện không ghi
/// được gì.
/// </para>
/// </summary>
public sealed class ListMyTenantsUseCase(
    IUserTenantRepository userTenants,
    ITenantRepository tenants,
    IClock clock)
{
    /// <param name="userId">
    /// Tài khoản đã được middleware phiên xác thực. Use case này không tự kiểm tra phiên:
    /// làm lại phép kiểm tra đó ở đây nghĩa là có hai chỗ cùng cài đặt BR-AUTH-022, và
    /// sớm muộn hai chỗ sẽ lệch nhau.
    /// </param>
    public async Task<IReadOnlyList<TenantSummaryDto>> ExecuteAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var tenantIds = await userTenants.ListTenantIdsAsync(userId, cancellationToken);
        if (tenantIds.Count == 0) return [];

        var now = clock.UtcNow;
        var found = await tenants.ListByIdsAsync(tenantIds, cancellationToken);

        return [.. found.Select(tenant => TenantMapper.ToSummary(tenant, now))];
    }
}
