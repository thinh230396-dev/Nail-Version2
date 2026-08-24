using NailManagement.Domain.Enums;

namespace NailManagement.Domain.Policies;

/// <summary>
/// Công thức tiền ở BR-INV-020 và phép suy ra trạng thái hóa đơn ở BR-PAY-003 — hạng mục
/// thứ tư trong bốn thứ mà lộ trình đánh dấu "tuyệt đối không cắt".
/// <para>
/// Toàn bộ là hàm thuần trên kiểu long VND (BR-VAL-003), nên viết test cho nó không cần
/// dựng database hay HTTP.
/// </para>
/// </summary>
public static class InvoiceMoneyPolicy
{
    /// <summary>Tổng tiền hàng: giá đã chốt tại thời điểm lập hóa đơn, nhân số lượng.</summary>
    public static long Subtotal(IEnumerable<(long UnitPrice, int Quantity)> lines)
        => lines.Sum(line => line.UnitPrice * line.Quantity);

    /// <summary>
    /// Tổng phải trả bằng tiền hàng trừ giảm giá cộng tip.
    /// BR-INV-022: tip nằm trong số tiền khách trả nhưng KHÔNG thuộc doanh thu tiệm —
    /// phép trừ tip đó nằm ở công thức doanh thu (BR-REV-001), không nằm ở đây.
    /// BR-INV-023: không có VAT, giá niêm yết đã bao gồm thuế.
    /// </summary>
    public static long Total(long subtotal, long discount, long tip) => subtotal - discount + tip;

    /// <summary>Tổng đã thu, tính cả dòng tiền cọc và dòng hoàn tiền mang số âm.</summary>
    public static long Collected(IEnumerable<long> paymentAmounts) => paymentAmounts.Sum();

    public static long Remaining(long total, long collected) => total - collected;

    /// <summary>
    /// BR-PAY-003 — trạng thái hóa đơn SUY RA từ tổng thu, không ai đặt tay.
    /// <para>
    /// Hai trạng thái Cancelled và Refunded không nằm trong phép suy này: chúng do người
    /// dùng chủ động đặt và ghi đè kết quả ở đây.
    /// </para>
    /// </summary>
    public static SalesInvoiceStatus ResolveStatus(long total, long collected)
    {
        if (total > 0 && collected >= total) return SalesInvoiceStatus.Paid;
        if (collected > 0) return SalesInvoiceStatus.Partial;

        return SalesInvoiceStatus.Pending;
    }
}
