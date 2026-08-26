using NailManagement.Domain.Common;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Policies;

namespace NailManagement.Domain.Entities.Salon;

/// <summary>
/// Hóa đơn khách trả cho tiệm — BR-INV-001, tách hoàn toàn khỏi hóa đơn đăng ký mà tiệm
/// trả cho SalonSys. Nhầm hai bảng này là nhầm luôn ý nghĩa của mọi con số doanh thu.
/// <para>
/// Đây là một gốc tổng hợp: dòng hóa đơn và dòng thu tiền chỉ được thêm qua các hàm ở đây,
/// để công thức tiền (BR-INV-020) và phép suy trạng thái (BR-PAY-003) luôn chạy lại ngay
/// sau mỗi thay đổi. Cho phép sửa hai bảng con từ bên ngoài là mở đường cho hóa đơn có
/// tổng tiền không khớp với các dòng của chính nó.
/// </para>
/// </summary>
public class SalesInvoice : ITenantOwned
{
    private readonly List<SalesInvoiceLine> _lines = [];
    private readonly List<InvoicePayment> _payments = [];

    /// <summary>Dành riêng cho EF Core.</summary>
    private SalesInvoice()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        BranchId = string.Empty;
        CustomerId = string.Empty;
        Code = string.Empty;
    }

    private SalesInvoice(
        string id,
        string tenantId,
        string branchId,
        string customerId,
        string? appointmentId,
        string? staffId,
        string code,
        string? note,
        string? createdByUserId,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        CustomerId = customerId;
        AppointmentId = appointmentId;
        StaffId = staffId;
        Code = code;
        Note = note;
        CreatedByUserId = createdByUserId;
        Status = SalesInvoiceStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Id { get; private set; }
    public string TenantId { get; private set; }

    /// <summary>BR-BRANCH-007 — hóa đơn thuộc riêng chi nhánh, là chiều "chi nhánh" của báo cáo doanh thu.</summary>
    public string BranchId { get; private set; }

    public Branch? Branch { get; private set; }

    /// <summary>BR-CUS-004 — mọi hóa đơn bắt buộc gắn một hồ sơ khách.</summary>
    public string CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    /// <summary>BR-INV-011 — rỗng nghĩa là khách mua lẻ, không đi từ lịch hẹn nào.</summary>
    public string? AppointmentId { get; private set; }

    public Appointment? Appointment { get; private set; }

    /// <summary>
    /// Kỹ thuật viên được ghi công cho hóa đơn này — chiều "nhân viên" của báo cáo doanh
    /// thu (BR-REV-004) và là nguồn tính hoa hồng ở BR-EMP-011.
    /// </summary>
    public string? StaffId { get; private set; }

    public Staff? Staff { get; private set; }

    /// <summary>BR-INV-016 — số hóa đơn dạng HD-yyyyMMdd-nnn, đánh theo từng tiệm và reset mỗi ngày.</summary>
    public string Code { get; private set; }

    public SalesInvoiceStatus Status { get; private set; }

    /// <summary>Tổng tiền hàng, tính lại từ các dòng sau mỗi thay đổi.</summary>
    public long Subtotal { get; private set; }

    /// <summary>BR-INV-021 — số tiền giảm nhập tay, không có mã voucher và không có phần trăm.</summary>
    public long Discount { get; private set; }

    public string? DiscountReason { get; private set; }

    /// <summary>BR-INV-022 — tip cộng vào tiền khách trả nhưng không thuộc doanh thu tiệm.</summary>
    public long Tip { get; private set; }

    public long Total { get; private set; }

    public string? Note { get; private set; }
    public string? CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset? RefundedAt { get; private set; }

    public IReadOnlyCollection<SalesInvoiceLine> Lines => _lines;
    public IReadOnlyCollection<InvoicePayment> Payments => _payments;

    /// <summary>Tổng đã thu, gồm cả tiền cọc và các dòng hoàn tiền mang số âm.</summary>
    public long Collected => InvoiceMoneyPolicy.Collected(_payments.Select(payment => payment.Amount));

    public long Remaining => InvoiceMoneyPolicy.Remaining(Total, Collected);

    public static SalesInvoice Create(
        string id,
        string tenantId,
        string branchId,
        string customerId,
        string? appointmentId,
        string? staffId,
        string code,
        string? note,
        string? createdByUserId,
        DateTimeOffset now)
        => new(
            Guard.Reference(id, "id", "Mã hóa đơn"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            Guard.Reference(branchId, "branchId", "Chi nhánh"),
            Guard.Reference(customerId, "customerId", "Khách hàng"),
            string.IsNullOrWhiteSpace(appointmentId) ? null : appointmentId.Trim(),
            string.IsNullOrWhiteSpace(staffId) ? null : staffId.Trim(),
            Guard.NotEmpty(code, "code", "Số hóa đơn", 40),
            Guard.Optional(note, "note", "Ghi chú hóa đơn", ValidationPolicy.LongTextMaxLength),
            createdByUserId,
            now);

    public void AddLine(string lineId, string? serviceId, string name, long unitPrice, int quantity, DateTimeOffset now)
    {
        EnsureEditable();

        _lines.Add(SalesInvoiceLine.Create(lineId, TenantId, Id, serviceId, name, unitPrice, quantity));
        Recalculate(now);
    }

    public void RemoveLine(string lineId, DateTimeOffset now)
    {
        EnsureEditable();

        var line = _lines.FirstOrDefault(candidate => candidate.Id == lineId);
        if (line is null) return;

        _lines.Remove(line);
        Recalculate(now);
    }

    /// <summary>BR-INV-021 — giảm giá phải nằm trong khoảng từ 0 đến tổng tiền hàng, kèm ô lý do.</summary>
    public void ApplyDiscount(long discount, string? reason, DateTimeOffset now)
    {
        EnsureEditable();

        if (discount < 0 || discount > Subtotal)
            throw DomainException.ForField("discount", "Số tiền giảm phải nằm trong khoảng từ 0 đến tổng tiền hàng.");

        Discount = discount;
        DiscountReason = Guard.Optional(reason, "discountReason", "Lý do giảm giá", ValidationPolicy.LongTextMaxLength);
        Recalculate(now);
    }

    public void SetTip(long tip, DateTimeOffset now)
    {
        EnsureEditable();

        Tip = Guard.Money(tip, "tip", "Tiền tip");
        Recalculate(now);
    }

    /// <summary>
    /// Ghi nhận một lần thu tiền rồi tính lại trạng thái theo BR-PAY-003.
    /// <para>
    /// Không chặn thu quá số còn lại: khách đưa dư rồi lấy lại tiền thừa là chuyện thường
    /// ở quầy, và ép cho khớp từng đồng chỉ khiến lễ tân ghi sai số cho xong.
    /// </para>
    /// </summary>
    public InvoicePayment RegisterPayment(
        string paymentId,
        PaymentType type,
        PaymentMethod method,
        long amount,
        DateTimeOffset paidAt,
        string? reference,
        string? createdByUserId,
        DateTimeOffset now)
    {
        if (Status is SalesInvoiceStatus.Cancelled or SalesInvoiceStatus.Refunded)
            throw DomainException.ForField("status", "Hóa đơn đã hủy hoặc đã hoàn tiền thì không thu thêm được.");

        var payment = InvoicePayment.Receive(
            paymentId, TenantId, Id, type, method, amount, paidAt, reference, createdByUserId);

        _payments.Add(payment);
        Recalculate(now);

        return payment;
    }

    /// <summary>
    /// BR-PAY-006/008 — hoàn tiền tạo một dòng âm kèm lý do, và tổng hoàn không được vượt
    /// tổng đã thu. BR-INV-014: hóa đơn đã thanh toán không sửa và không hủy được, sai sót
    /// chỉ xử lý bằng đường này.
    /// </summary>
    public InvoicePayment IssueRefund(
        string paymentId,
        PaymentMethod method,
        long amount,
        DateTimeOffset paidAt,
        string reason,
        string? createdByUserId,
        DateTimeOffset now)
    {
        if (Status != SalesInvoiceStatus.Paid)
            throw DomainException.ForField("status", "Chỉ hoàn tiền được hóa đơn đã thanh toán đủ.");

        if (amount > Collected)
            throw DomainException.ForField("amount", "Số tiền hoàn không được vượt quá số tiền đã thu.");

        var refund = InvoicePayment.Refund(
            paymentId, TenantId, Id, method, amount, paidAt, reason, createdByUserId);

        _payments.Add(refund);

        // Trạng thái Refunded do người dùng chủ động đặt, nên nó ghi đè kết quả của phép
        // suy ở BR-PAY-003 thay vì quay ngược về Partial.
        Status = SalesInvoiceStatus.Refunded;
        RefundedAt = paidAt;
        UpdatedAt = now;

        return refund;
    }

    /// <summary>BR-INV-015 — hóa đơn chưa thu đủ thì hủy được; BR-INV-014 cấm hủy hóa đơn đã thanh toán.</summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status is SalesInvoiceStatus.Paid or SalesInvoiceStatus.Refunded)
            throw DomainException.ForField("status", "Hóa đơn đã thanh toán thì không hủy được, chỉ hoàn tiền.");

        Status = SalesInvoiceStatus.Cancelled;
        CancelledAt = now;
        UpdatedAt = now;
    }

    /// <summary>BR-PAY-003 — hóa đơn đã thu đủ tiền, là điều kiện để lịch hẹn tự hoàn tất (BR-APT-026).</summary>
    public bool IsFullyPaid() => Status == SalesInvoiceStatus.Paid;

    private void Recalculate(DateTimeOffset now)
    {
        Subtotal = InvoiceMoneyPolicy.Subtotal(_lines.Select(line => (line.UnitPrice, line.Quantity)));

        // Giảm giá đang lớn hơn tổng tiền hàng mới thì kéo về đúng trần, thay vì để hóa
        // đơn mang tổng tiền âm — chuyện này xảy ra khi xóa bớt một dòng sau khi đã giảm giá.
        if (Discount > Subtotal) Discount = Subtotal;

        Total = InvoiceMoneyPolicy.Total(Subtotal, Discount, Tip);

        if (Status is not (SalesInvoiceStatus.Cancelled or SalesInvoiceStatus.Refunded))
            Status = InvoiceMoneyPolicy.ResolveStatus(Total, Collected);

        UpdatedAt = now;
    }

    private void EnsureEditable()
    {
        // BR-INV-014/015 — chỉ hóa đơn chưa thu đủ mới sửa được.
        if (Status is SalesInvoiceStatus.Paid or SalesInvoiceStatus.Refunded or SalesInvoiceStatus.Cancelled)
            throw DomainException.ForField("status", $"Hóa đơn ở trạng thái {Status} thì không sửa được nữa.");
    }
}
