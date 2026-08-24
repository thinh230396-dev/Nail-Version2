namespace NailManagement.Domain.Enums;

/// <summary>
/// BR-TENANT-002 — database chỉ lưu HAI trạng thái do người đặt.
/// <para>
/// <c>TRIAL</c> và <c>OVERDUE</c> mà giao diện hiển thị KHÔNG nằm ở đây: chúng được tính
/// lúc đọc từ <c>ExpiresAt</c> và <c>IsTrial</c> (xem <see cref="TenantDisplayStatus"/>).
/// Lưu chúng vào cột thì phải có job chạy nền để cập nhật, mà BR-TENANT-003 đã loại bỏ
/// mọi job chạy nền khỏi hệ thống.
/// </para>
/// </summary>
public enum TenantStatus
{
    Active = 1,
    Suspended = 2
}
