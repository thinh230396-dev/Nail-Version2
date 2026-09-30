using NailManagement.Domain.Access;

namespace NailManagement.Domain.Salon.StaffMembers;

/// <summary>
/// BR-EMP-002 — nhân viên có hai vai trò nghiệp vụ.
/// <para>
/// Đây KHÔNG phải vai trò đăng nhập (<c>UserRole</c>). BR-AUTH-002: kỹ thuật viên không
/// có tài khoản; chỉ lễ tân mới được cấp tài khoản đăng nhập (BR-AUTH-013).
/// </para>
/// </summary>
public enum StaffRole
{
    Technician = 1,
    Receptionist = 2
}
