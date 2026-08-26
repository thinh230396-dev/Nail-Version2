using NailManagement.Domain.Common;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Policies;

namespace NailManagement.Domain.Entities.Salon;

/// <summary>
/// Một lần ghi nhận tiền trên hóa đơn bán hàng — BR-PAY-001.
/// <para>
/// BR-PAY-004 — chia nhiều phương thức trong một lần thu được hỗ trợ tự nhiên vì mỗi
/// phương thức là một dòng riêng ở đây, chứ hóa đơn không có cột "phương thức thanh toán".
/// Khách trả nửa tiền mặt nửa chuyển khoản là hai dòng, không phải một trường hợp đặc biệt.
/// </para>
/// </summary>
public class InvoicePayment : ITenantOwned
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private InvoicePayment()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        InvoiceId = string.Empty;
    }

    private InvoicePayment(
        string id,
        string tenantId,
        string invoiceId,
        PaymentType type,
        PaymentMethod method,
        long amount,
        DateTimeOffset paidAt,
        string? reference,
        string? reason,
        string? createdByUserId)
    {
        Id = id;
        TenantId = tenantId;
        InvoiceId = invoiceId;
        Type = type;
        Method = method;
        Amount = amount;
        PaidAt = paidAt;
        Reference = reference;
        Reason = reason;
        CreatedByUserId = createdByUserId;
    }

    public string Id { get; private set; }
    public string TenantId { get; private set; }
    public string InvoiceId { get; private set; }
    public PaymentType Type { get; private set; }
    public PaymentMethod Method { get; private set; }

    /// <summary>
    /// Số tiền, VND. Dòng hoàn tiền mang giá trị ÂM (BR-PAY-006), nhờ đó công thức doanh
    /// thu ở BR-REV-001 chỉ cần cộng dồn là đã tự trừ phần đã trả lại cho khách.
    /// </summary>
    public long Amount { get; private set; }

    public DateTimeOffset PaidAt { get; private set; }

    /// <summary>Mã giao dịch của ngân hàng hoặc ví, do lễ tân nhập tay (BR-PAY-005).</summary>
    public string? Reference { get; private set; }

    /// <summary>BR-PAY-006 — lý do bắt buộc khi hoàn tiền.</summary>
    public string? Reason { get; private set; }

    public string? CreatedByUserId { get; private set; }

    public SalesInvoice? Invoice { get; private set; }

    /// <summary>Tiền khách trả vào: thu thường hoặc tiền cọc chuyển sang (BR-APT-031).</summary>
    public static InvoicePayment Receive(
        string id,
        string tenantId,
        string invoiceId,
        PaymentType type,
        PaymentMethod method,
        long amount,
        DateTimeOffset paidAt,
        string? reference,
        string? createdByUserId)
    {
        if (type == PaymentType.Refund)
            throw DomainException.ForField("type", "Hoàn tiền phải dùng hàm hoàn tiền riêng.");

        if (amount <= 0)
            throw DomainException.ForField("amount", "Số tiền thu phải lớn hơn 0.");

        return new InvoicePayment(
            Guard.Reference(id, "id", "Mã dòng thu tiền"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            Guard.Reference(invoiceId, "invoiceId", "Hóa đơn"),
            type,
            method,
            amount,
            paidAt,
            Guard.Optional(reference, "reference", "Mã giao dịch", 120),
            null,
            createdByUserId);
    }

    /// <summary>
    /// BR-PAY-006 — hoàn tiền là một dòng mang số âm kèm lý do bắt buộc, không phải là
    /// sửa hay xóa dòng thu cũ. Giữ đủ cả hai chiều tiền thì sổ sách mới đối chiếu được.
    /// </summary>
    public static InvoicePayment Refund(
        string id,
        string tenantId,
        string invoiceId,
        PaymentMethod method,
        long amount,
        DateTimeOffset paidAt,
        string reason,
        string? createdByUserId)
    {
        if (amount <= 0)
            throw DomainException.ForField("amount", "Số tiền hoàn phải lớn hơn 0.");

        return new InvoicePayment(
            Guard.Reference(id, "id", "Mã dòng hoàn tiền"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            Guard.Reference(invoiceId, "invoiceId", "Hóa đơn"),
            PaymentType.Refund,
            method,
            -amount,
            paidAt,
            null,
            Guard.NotEmpty(reason, "reason", "Lý do hoàn tiền", ValidationPolicy.LongTextMaxLength),
            createdByUserId);
    }
}
