using NailManagement.Application.DTOs;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Enums.Platform;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.Mappings;

/// <summary>
/// Chuyển entity <see cref="Tenant"/> sang DTO gửi ra ngoài.
/// <para>
/// Mọi hàm ở đây nhận thêm tham số thời điểm, vì trạng thái hiển thị và cờ chỉ-đọc đều là
/// kết quả tính lúc đọc (BR-TENANT-002, BR-TENANT-010). Không có "trạng thái tiệm" nào
/// đứng yên để mà chép thẳng ra.
/// </para>
/// </summary>
public static class TenantMapper
{
    /// <summary>
    /// Bốn chuỗi trạng thái gửi cho frontend. Frontend đang dùng đúng cách viết này
    /// (<c>Tenant['status']</c> trong <c>src/types.ts</c>), nên không được đổi.
    /// </summary>
    public static string ToWireFormat(TenantDisplayStatus status) => status switch
    {
        TenantDisplayStatus.Trial => "TRIAL",
        TenantDisplayStatus.Active => "ACTIVE",
        TenantDisplayStatus.Overdue => "OVERDUE",
        TenantDisplayStatus.Suspended => "SUSPENDED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái tiệm không hợp lệ.")
    };

    public static string ToWireFormat(AccountStatus status) => status switch
    {
        AccountStatus.Active => "ACTIVE",
        AccountStatus.Suspended => "SUSPENDED",
        AccountStatus.Inactive => "INACTIVE",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái tài khoản không hợp lệ.")
    };

    public static TenantSummaryDto ToSummary(Tenant tenant, DateTimeOffset now) => new(
        tenant.Id,
        tenant.Code,
        tenant.Name,
        ToWireFormat(tenant.DisplayStatusAt(now)),
        tenant.IsReadOnlyAt(now),
        tenant.ExpiresAt);

    /// <param name="package">
    /// Gói của tiệm. Bắt buộc có: thiếu nó thì không trả lời được bước 2 của BR-TENANT-013,
    /// và bỏ qua bước đó nghĩa là mọi tiệm đều dùng được mọi tính năng.
    /// </param>
    public static TenantScopeDto ToScope(Tenant tenant, Package package, DateTimeOffset now) => new(
        tenant.Id,
        tenant.Code,
        tenant.Name,
        ToWireFormat(tenant.DisplayStatusAt(now)),
        tenant.IsReadOnlyAt(now),
        package.Id,
        package.Name,
        PackageMapper.ReadCapabilities(package));

    /// <param name="usage">
    /// Số chi nhánh và nhân viên đang hoạt động. Cho phép null vì có đúng một lúc chưa đếm
    /// tới: ngay sau khi tạo tiệm, khi cả hai con số còn bằng đúng những gì lệnh tạo vừa ghi.
    /// </param>
    public static TenantDetailDto ToDetail(
        Tenant tenant,
        Package package,
        TenantUsage? usage,
        IReadOnlyList<TenantOwnerDto> owners,
        DateTimeOffset now) => new(
        tenant.Id,
        tenant.Code,
        tenant.Name,
        ToWireFormat(tenant.DisplayStatusAt(now)),
        tenant.IsReadOnlyAt(now),
        tenant.IsTrial,
        tenant.ExpiresAt,
        DaysRemaining(tenant.ExpiresAt, now),
        tenant.Address,
        tenant.Phone,
        tenant.ContactEmail,
        tenant.Timezone,
        package.Id,
        package.Name,
        tenant.SubscriptionPrice,
        tenant.SubscriptionPackageVersion,
        PackageMapper.ToWireFormat(tenant.BillingCycle),
        tenant.SubscriptionStartedAt,
        package.MaxSalons,
        package.MaxStaff,
        usage?.ActiveBranches ?? 0,
        usage?.ActiveStaff ?? 0,
        owners,
        tenant.CreatedAt,
        tenant.UpdatedAt);

    /// <summary>
    /// Chủ tiệm hiện trong danh sách của Superadmin.
    /// <para>
    /// Chỉ năm trường, và không trường nào chạm tới mật khẩu — cùng lý do với
    /// <see cref="AccountMapper"/>: DTO là hàng rào giữ những cột đó ở lại máy chủ.
    /// </para>
    /// </summary>
    public static TenantOwnerDto ToOwner(AppUser user) => new(
        user.Id,
        user.Email.Value,
        user.Username,
        user.DisplayName,
        ToWireFormat(user.Status));

    /// <summary>
    /// Số ngày còn lại, làm tròn xuống.
    /// <para>
    /// Làm tròn xuống chứ không lên là có chủ đích: tiệm hết hạn sau 12 giờ nữa phải đọc ra
    /// 0 — "hết hạn hôm nay" — chứ không phải 1. Và tiệm đã quá hạn cho ra số âm, để màn
    /// hình nói được "quá hạn 3 ngày" thay vì im lặng hiển thị số 0 như thể vẫn còn kịp.
    /// </para>
    /// </summary>
    private static int DaysRemaining(DateTimeOffset expiresAt, DateTimeOffset now)
        => (int)Math.Floor((expiresAt - now).TotalDays);
}
