using NailManagement.Application.DTOs;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Enums.Platform;

namespace NailManagement.Application.Mappings;

/// <summary>Chuyển hóa đơn đăng ký sang DTO gửi ra ngoài — BR-INV-030…033.</summary>
public static class SubscriptionInvoiceMapper
{
    /// <summary>
    /// Ba trạng thái của lược đồ, cộng một trạng thái <b>tính lúc đọc</b>.
    ///
    /// <para>
    /// <c>OVERDUE</c> không có trong enum và cũng không nên có: nó chỉ là "chưa trả mà đã quá
    /// hạn", suy được từ <c>DueAt</c> tại thời điểm đọc. Lưu nó thành một cột là dựng ra một
    /// trạng thái cần ai đó đi cập nhật mỗi đêm — mà BR-TENANT-003 nói toàn hệ thống không có
    /// job chạy nền nào.
    /// </para>
    /// </summary>
    public static string ToWireFormat(SubscriptionInvoiceStatus status, DateTimeOffset dueAt, DateTimeOffset now)
        => status switch
        {
            SubscriptionInvoiceStatus.Paid => "PAID",
            SubscriptionInvoiceStatus.Cancelled => "CANCELLED",
            SubscriptionInvoiceStatus.Pending => now > dueAt ? "OVERDUE" : "PENDING",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái hóa đơn đăng ký không hợp lệ.")
        };

    public static SubscriptionInvoiceDto ToDto(SubscriptionInvoice invoice, DateTimeOffset now)
        => new(
            invoice.Id,
            invoice.Code,
            invoice.TenantId,
            invoice.TenantName,
            invoice.PackageId,
            invoice.PackageName,
            invoice.Amount,
            PackageMapper.ToWireFormat(invoice.BillingCycle),
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.DueAt,
            invoice.Reason,
            ToWireFormat(invoice.Status, invoice.DueAt, now),
            invoice.PaymentReference,
            invoice.PaymentNote,
            invoice.SubmittedAt,
            invoice.PaidAt,
            invoice.CreatedAt);
}
