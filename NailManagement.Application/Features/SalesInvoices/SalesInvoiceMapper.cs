using NailManagement.Domain.Salon.Invoices;
using NailManagement.Domain.Shared;

namespace NailManagement.Application.Features.SalesInvoices;

/// <summary>
/// Chuyển entity <see cref="SalesInvoice"/> sang DTO gửi ra ngoài, và đọc ngược các chuỗi
/// trạng thái, loại thu và phương thức từ client.
/// </summary>
public static class SalesInvoiceMapper
{
    /// <summary>BR-INV-013 — năm trạng thái, đã bỏ <c>FAILED</c> vì không có cổng thanh toán thật.</summary>
    public static string ToWireFormat(SalesInvoiceStatus status) => status switch
    {
        SalesInvoiceStatus.Pending => "PENDING",
        SalesInvoiceStatus.Partial => "PARTIAL",
        SalesInvoiceStatus.Paid => "PAID",
        SalesInvoiceStatus.Refunded => "REFUNDED",
        SalesInvoiceStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái hóa đơn không hợp lệ.")
    };

    /// <summary>BR-PAY-002 — ba loại dòng thu.</summary>
    public static string ToWireFormat(PaymentType type) => type switch
    {
        PaymentType.Deposit => "DEPOSIT",
        PaymentType.Payment => "PAYMENT",
        PaymentType.Refund => "REFUND",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Loại thu tiền không hợp lệ.")
    };

    /// <summary>BR-PAY-005 — năm phương thức, đều chỉ là nhãn ghi nhận thủ công.</summary>
    public static string ToWireFormat(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "CASH",
        PaymentMethod.Bank => "BANK",
        PaymentMethod.Card => "CARD",
        PaymentMethod.Momo => "MOMO",
        PaymentMethod.ZaloPay => "ZALOPAY",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Phương thức thanh toán không hợp lệ.")
    };

    public static PaymentMethod ParseMethod(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "CASH" => PaymentMethod.Cash,
            "BANK" => PaymentMethod.Bank,
            "CARD" => PaymentMethod.Card,
            "MOMO" => PaymentMethod.Momo,
            "ZALOPAY" => PaymentMethod.ZaloPay,
            _ => throw DomainException.ForField(
                "method", "Phương thức thanh toán chỉ nhận CASH, BANK, CARD, MOMO hoặc ZALOPAY.")
        };

    /// <summary>
    /// Đọc trạng thái người dùng muốn đặt.
    /// <para>
    /// Từ chối ngay ba trạng thái suy ra được: BR-PAY-003 nói chúng là <b>kết quả</b> của tổng
    /// thu, nên nhận chúng từ client là cho phép đánh dấu một hóa đơn chưa thu đồng nào thành
    /// đã thanh toán. <c>REFUNDED</c> cũng bị từ chối ở đây vì nó phải đi kèm một dòng tiền âm
    /// và một lý do, tức là qua đường hoàn tiền chứ không qua đường đổi trạng thái.
    /// </para>
    /// </summary>
    public static SalesInvoiceStatus ParseSettableStatus(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "CANCELLED" => SalesInvoiceStatus.Cancelled,

            "PENDING" or "PARTIAL" or "PAID" => throw DomainException.ForField(
                "status",
                "Ba trạng thái PENDING, PARTIAL và PAID được suy ra từ số tiền đã thu nên không đặt tay được."),

            "REFUNDED" => throw DomainException.ForField(
                "status", "Hoàn tiền phải đi qua lệnh hoàn tiền để ghi nhận số tiền và lý do."),

            _ => throw DomainException.ForField("status", "Trạng thái hóa đơn chỉ nhận CANCELLED.")
        };

    /// <summary>
    /// Dựng DTO từ hóa đơn <b>đã được nạp kèm</b> dòng hóa đơn, dòng thu tiền, chi nhánh và
    /// khách hàng.
    /// <para>
    /// Thiếu một phép nối là lỗi lập trình chứ không phải lỗi nghiệp vụ, nên nó ném ra
    /// <see cref="InvalidOperationException"/> kèm tên bảng còn thiếu — hỏng ngay và nói rõ,
    /// thay vì lặng lẽ trả về một hóa đơn không tên khách mà người dùng phải là người phát hiện.
    /// </para>
    /// </summary>
    public static SalesInvoiceDto ToDto(SalesInvoice invoice)
    {
        var branch = invoice.Branch ?? throw NotLoaded(invoice.Id, nameof(SalesInvoice.Branch));
        var customer = invoice.Customer ?? throw NotLoaded(invoice.Id, nameof(SalesInvoice.Customer));

        return new SalesInvoiceDto(
            invoice.Id,
            invoice.TenantId,
            invoice.BranchId,
            branch.Name,
            invoice.CustomerId,
            customer.FullName,
            customer.Phone.Value,
            invoice.AppointmentId,
            invoice.StaffId,
            invoice.Staff?.FullName,
            invoice.Code,
            ToWireFormat(invoice.Status),
            invoice.Subtotal,
            invoice.Discount,
            invoice.DiscountReason,
            invoice.Tip,
            invoice.Total,
            invoice.Collected,
            invoice.Remaining,
            invoice.Note,
            [.. invoice.Lines
                .OrderBy(line => line.Name, StringComparer.CurrentCulture)
                .Select(line => new SalesInvoiceLineDto(
                    line.Id, line.ServiceId, line.Name, line.UnitPrice, line.Quantity, line.LineTotal))],

            // Dòng thu sắp theo thời gian: tiền cọc ghi lúc lập hóa đơn luôn đứng trước các
            // lần thu sau, và một dòng hoàn tiền luôn nằm cuối. Đó cũng là thứ tự mà người
            // đọc một biên lai mong thấy.
            [.. invoice.Payments
                .OrderBy(payment => payment.PaidAt)
                .ThenBy(payment => payment.Id)
                .Select(payment => new InvoicePaymentDto(
                    payment.Id,
                    ToWireFormat(payment.Type),
                    ToWireFormat(payment.Method),
                    payment.Amount,
                    payment.PaidAt,
                    payment.Reference,
                    payment.Reason))],
            invoice.CreatedAt,
            invoice.UpdatedAt);
    }

    private static InvalidOperationException NotLoaded(string invoiceId, string navigation)
        => new($"Hóa đơn {invoiceId} được đọc mà chưa nạp kèm {navigation}. "
               + "Truy vấn ở tầng lưu trữ phải Include bảng này trước khi dựng DTO.");
}
