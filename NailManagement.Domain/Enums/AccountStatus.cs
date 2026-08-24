namespace NailManagement.Domain.Enums;

/// <summary>
/// BR-AUTH-020 — tài khoản có 3 trạng thái.
/// <c>Inactive</c> là kết quả của thao tác "xóa": BR-DEL-001 quy định không có gì bị
/// xóa cứng khỏi database trong toàn hệ thống.
/// </summary>
public enum AccountStatus
{
    /// <summary>Bình thường, đăng nhập được.</summary>
    Active = 1,

    /// <summary>Khóa tạm, mở lại được.</summary>
    Suspended = 2,

    /// <summary>Đã vô hiệu vĩnh viễn.</summary>
    Inactive = 3
}
