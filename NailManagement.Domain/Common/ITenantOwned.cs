namespace NailManagement.Domain.Common;

/// <summary>
/// Đánh dấu một entity thuộc về đúng một tiệm.
/// <para>
/// BR-ISO-001 — mọi bảng nghiệp vụ tầng salon bắt buộc có <c>TenantId</c> không rỗng và
/// có index. Giao diện này biến quy tắc đó thành thứ máy kiểm tra được: tầng lưu trữ duyệt
/// mọi entity mang giao diện này và tự gắn bộ lọc theo tiệm đang làm việc, nên không
/// endpoint nào phải tự viết điều kiện lọc — đó chính là yêu cầu của BR-ISO-002.
/// </para>
/// <para>
/// Ba bảng nền tảng <c>Tenants</c>, <c>Packages</c>, <c>SubscriptionInvoices</c> cố ý KHÔNG
/// mang giao diện này: chúng thuộc tầng Superadmin và được lọc theo quy tắc riêng.
/// </para>
/// </summary>
public interface ITenantOwned
{
    string TenantId { get; }
}
