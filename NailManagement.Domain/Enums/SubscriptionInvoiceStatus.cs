namespace NailManagement.Domain.Enums;

/// <summary>
/// Trạng thái hóa đơn đăng ký (tenant trả tiền cho SalonSys).
/// <para>
/// Khác hẳn <see cref="SalesInvoiceStatus"/> — BR-INV-001 quy định hai loại hóa đơn nằm ở
/// hai bảng tách biệt và không được nhầm lẫn. Hóa đơn đăng ký không có <c>Partial</c> vì
/// tenant chuyển khoản trọn gói, cũng không có <c>Refunded</c> vì BR-INV-031 cấm xóa và
/// không có nghiệp vụ hoàn tiền gói ở MVP.
/// </para>
/// </summary>
public enum SubscriptionInvoiceStatus
{
    Pending = 1,
    Paid = 2,
    Cancelled = 3
}
