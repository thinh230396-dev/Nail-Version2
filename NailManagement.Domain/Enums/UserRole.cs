namespace NailManagement.Domain.Enums;

/// <summary>
/// BR-AUTH-001 — hệ thống có đúng 3 vai trò đăng nhập.
/// BR-AUTH-002: kỹ thuật viên KHÔNG có tài khoản, họ chỉ là dữ liệu nhân sự.
/// BR-AUTH-003: không có vai trò khách hàng. BR-AUTH-004: không có vai trò hỗ trợ.
/// </summary>
public enum UserRole
{
    SuperAdmin = 1,
    TenantAdmin = 2,
    Receptionist = 3
}
