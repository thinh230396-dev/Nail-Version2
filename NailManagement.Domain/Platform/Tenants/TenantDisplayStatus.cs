namespace NailManagement.Domain.Platform.Tenants;

/// <summary>
/// BR-TENANT-001 — bốn trạng thái tenant mà người dùng nhìn thấy. Đây là kết quả TÍNH RA,
/// không phải cột trong database. Bỏ <c>EXPIRING</c> so với bản frontend cũ.
/// </summary>
public enum TenantDisplayStatus
{
    Trial = 1,
    Active = 2,
    Overdue = 3,
    Suspended = 4
}
