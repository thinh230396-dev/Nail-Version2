namespace NailManagement.Domain.Enums;

/// <summary>
/// BR-INV-013 — năm trạng thái hóa đơn bán hàng. Bỏ <c>FAILED</c> vì không có cổng thanh
/// toán thật nên không có giao dịch thất bại.
/// <para>
/// Ba trạng thái <c>Pending</c>, <c>Partial</c>, <c>Paid</c> KHÔNG được đặt tay — chúng
/// suy ra từ tổng tiền đã thu theo BR-PAY-003.
/// </para>
/// </summary>
public enum SalesInvoiceStatus
{
    Pending = 1,
    Partial = 2,
    Paid = 3,
    Refunded = 4,
    Cancelled = 5
}
