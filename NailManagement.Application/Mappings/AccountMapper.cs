using NailManagement.Application.DTOs;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.Application.Mappings;

/// <summary>
/// Chuyển entity <see cref="AppUser"/> sang DTO gửi ra ngoài.
/// <para>
/// Đây là chỗ duy nhất quyết định trường nào của tài khoản được phép rời khỏi máy chủ. Mọi
/// trường không liệt kê ở đây — <c>PasswordHash</c>, <c>PasswordSalt</c>,
/// <c>FailedAttempts</c>, <c>LockedUntil</c> — mặc nhiên bị bỏ lại.
/// </para>
/// </summary>
public static class AccountMapper
{
    /// <summary>
    /// Tên vai trò gửi cho frontend. Frontend đang dùng đúng ba chuỗi này
    /// (<c>PortalRole</c> trong <c>src/auth/demoAccounts.ts</c>), nên không được đổi cách viết.
    /// </summary>
    public static string ToWireFormat(UserRole role) => role switch
    {
        UserRole.SuperAdmin => "SUPERADMIN",
        UserRole.TenantAdmin => "TENANT_ADMIN",
        UserRole.Receptionist => "RECEPTIONIST",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Vai trò không hợp lệ.")
    };

    public static AccountDto ToDto(AppUser user) => new()
    {
        Id = user.Id,
        Email = user.Email.Value,
        Role = ToWireFormat(user.Role),
        DisplayName = user.DisplayName
    };

    /// <summary>
    /// Tài khoản chủ tiệm kèm danh sách tiệm đang giữ.
    /// <para>
    /// Trạng thái mượn lại cách viết của <see cref="TenantMapper"/> thay vì dựng bảng thứ
    /// hai: cùng một enum mà có hai cách viết ra dây là cách chắc chắn nhất để hai màn hình
    /// hiển thị khác nhau cho cùng một tài khoản.
    /// </para>
    /// </summary>
    public static TenantAdminAccountDto ToTenantAdminAccount(
        AppUser user, IReadOnlyList<string> tenantIds) => new(
        user.Id,
        user.Email.Value,
        user.Username,
        user.DisplayName,
        TenantMapper.ToWireFormat(user.Status),
        user.CreatedAt,
        tenantIds);
}
