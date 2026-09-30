using NailManagement.Domain.Salon.Invoices;

namespace NailManagement.UnitTests.Invoices;

/// <summary>
/// Hóa đơn tự giữ công thức tiền (BR-INV-020) và tự suy trạng thái từ tổng thu (BR-PAY-003).
/// Phép thử HTTP ở <c>InvoiceMoneyTests</c> kiểm cùng các luật qua cả đường ống; ở đây kiểm thẳng
/// aggregate, nên một công thức gãy báo đỏ trong vài mili giây và chỉ đúng vào chỗ gãy.
/// </summary>
public sealed class SalesInvoiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 9, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Tong_tien_bang_tien_hang_tru_giam_gia_cong_tip()
    {
        var invoice = Invoice((150_000, 2), (80_000, 1));

        invoice.ApplyDiscount(30_000, "Khách quen", Now);
        invoice.SetTip(20_000, Now);

        Assert.Equal(380_000, invoice.Subtotal);
        Assert.Equal(370_000, invoice.Total);
        Assert.Equal(370_000, invoice.Remaining);
    }

    [Fact]
    public void Trang_thai_suy_ra_tu_so_tien_da_thu()
    {
        var invoice = Invoice((200_000, 1));
        Assert.Equal(SalesInvoiceStatus.Pending, invoice.Status);

        Pay(invoice, 50_000);
        Assert.Equal(SalesInvoiceStatus.Partial, invoice.Status);

        Pay(invoice, 150_000);
        Assert.Equal(SalesInvoiceStatus.Paid, invoice.Status);
        Assert.Equal(0, invoice.Remaining);
        Assert.True(invoice.IsFullyPaid());
    }

    [Fact]
    public void Khong_thu_qua_so_con_thieu()
    {
        var invoice = Invoice((100_000, 1));
        Pay(invoice, 60_000);

        DomainAssert.RejectsField("amount", () => Pay(invoice, 40_001));
        Assert.Equal(60_000, invoice.Collected);
    }

    [Fact]
    public void Giam_gia_khong_duoc_am_hay_vuot_tien_hang()
    {
        var invoice = Invoice((100_000, 1));

        DomainAssert.RejectsField("discount", () => invoice.ApplyDiscount(100_001, null, Now));
        DomainAssert.RejectsField("discount", () => invoice.ApplyDiscount(-1, null, Now));
    }

    [Fact]
    public void Bo_dong_hang_sau_khi_giam_gia_thi_giam_gia_keo_ve_bang_tien_hang()
    {
        // Không kéo về thì hóa đơn mang tổng tiền âm.
        var invoice = Invoice((100_000, 1), (300_000, 1));
        invoice.ApplyDiscount(250_000, "Khuyến mãi", Now);

        invoice.RemoveLine("LIN-1", Now);

        Assert.Equal(100_000, invoice.Discount);
        Assert.Equal(0, invoice.Total);
        Assert.Equal(SalesInvoiceStatus.Pending, invoice.Status);
    }

    [Fact]
    public void Hoa_don_da_thu_du_thi_khong_sua_duoc()
    {
        var invoice = Invoice((100_000, 1));
        Pay(invoice, 100_000);

        DomainAssert.RejectsField("status", () => invoice.AddLine("LIN-X", null, "Thêm", 10_000, 1, Now));
        DomainAssert.RejectsField("status", () => invoice.SetTip(10_000, Now));
    }

    [Fact]
    public void Hoan_tien_la_dong_so_am_va_dua_hoa_don_ve_trang_thai_da_hoan()
    {
        var invoice = Invoice((300_000, 1));
        Pay(invoice, 300_000);

        var refund = invoice.IssueRefund("REF-1", PaymentMethod.Cash, 100_000, Now, "Làm lại bộ móng", "USR-1", Now);

        Assert.Equal(-100_000, refund.Amount);
        Assert.Equal(PaymentType.Refund, refund.Type);
        Assert.Equal(SalesInvoiceStatus.Refunded, invoice.Status);
        Assert.Equal(200_000, invoice.Collected);

        // Đã hoàn tiền là điểm cuối: không thu thêm được.
        DomainAssert.RejectsField("status", () => Pay(invoice, 1));
    }

    [Fact]
    public void Chi_hoan_duoc_hoa_don_da_thu_du_va_khong_qua_so_da_thu()
    {
        var partial = Invoice((300_000, 1));
        Pay(partial, 100_000);
        DomainAssert.RejectsField("status",
            () => partial.IssueRefund("REF-1", PaymentMethod.Cash, 50_000, Now, "Lý do", null, Now));

        var paid = Invoice((300_000, 1));
        Pay(paid, 300_000);
        DomainAssert.RejectsField("amount",
            () => paid.IssueRefund("REF-2", PaymentMethod.Cash, 300_001, Now, "Lý do", null, Now));
    }

    [Fact]
    public void Hoan_tien_bat_buoc_co_ly_do()
    {
        var invoice = Invoice((100_000, 1));
        Pay(invoice, 100_000);

        DomainAssert.RejectsField("reason",
            () => invoice.IssueRefund("REF-1", PaymentMethod.Cash, 10_000, Now, "   ", null, Now));
    }

    [Fact]
    public void Hoa_don_da_thanh_toan_khong_huy_duoc_hoa_don_chua_thu_thi_huy_duoc()
    {
        var paid = Invoice((100_000, 1));
        Pay(paid, 100_000);
        DomainAssert.RejectsField("status", () => paid.Cancel(Now));

        var pending = Invoice((100_000, 1));
        pending.Cancel(Now);
        Assert.Equal(SalesInvoiceStatus.Cancelled, pending.Status);
        DomainAssert.RejectsField("status", () => Pay(pending, 10_000));
    }

    private static SalesInvoice Invoice(params (long Price, int Quantity)[] lines)
    {
        var invoice = SalesInvoice.Create("INV-1", "TEN-1", "BRN-1", "CUS-1",
            null, null, "HD-20260930-001", null, "USR-1", Now);

        for (var index = 0; index < lines.Length; index++)
            invoice.AddLine($"LIN-{index}", null, $"Dịch vụ {index}", lines[index].Price, lines[index].Quantity, Now);

        return invoice;
    }

    private static void Pay(SalesInvoice invoice, long amount)
        => invoice.RegisterPayment($"PAY-{invoice.Payments.Count}", PaymentType.Payment,
            PaymentMethod.Cash, amount, Now, null, "USR-1", Now);
}
