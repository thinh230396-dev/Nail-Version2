using NailManagement.Domain.Common;

namespace NailManagement.Domain.Entities;

/// <summary>
/// Bảng nối cho phép một tài khoản chủ tiệm quản lý nhiều tiệm — BR-AUTH-023.
/// <para>
/// Đây là chỗ dễ lộ dữ liệu chéo tiệm nhất trong toàn hệ thống, nên trình tự ở BR-ISO-003
/// không được rút gọn: lấy tiệm đang làm việc từ phiên, kiểm tra tài khoản có liên kết ở
/// bảng này, rồi mới lọc dữ liệu. Bỏ bước giữa là mở đường cho một chủ tiệm đọc dữ liệu
/// của tiệm mà họ không quản lý, chỉ bằng cách đổi một mã trong phiên.
/// </para>
/// <para>
/// BR-TENANT-021 — xóa mềm một tiệm chỉ gỡ liên kết ở đây; tài khoản chỉ bị chặn đăng nhập
/// khi không còn tiệm nào.
/// </para>
/// </summary>
public class UserTenant
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private UserTenant()
    {
        UserId = string.Empty;
        TenantId = string.Empty;
    }

    private UserTenant(string userId, string tenantId, DateTimeOffset now)
    {
        UserId = userId;
        TenantId = tenantId;
        CreatedAt = now;
    }

    public string UserId { get; private set; }
    public string TenantId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public AppUser? User { get; private set; }
    public Tenant? Tenant { get; private set; }

    public static UserTenant Link(string userId, string tenantId, DateTimeOffset now)
        => new(
            Guard.Reference(userId, "userId", "Tài khoản"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            now);
}
