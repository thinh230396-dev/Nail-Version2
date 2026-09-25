using NailManagement.Domain.Platform.Packages;
using NailManagement.Domain.Platform.Tenants;
using NailManagement.Domain.Shared;

namespace NailManagement.Domain.Platform.Subscriptions;

/// <summary>
/// Hóa đơn tiệm trả cho SalonSys — BR-INV-001, do Superadmin quản lý.
/// <para>
/// Bảng này KHÔNG mang <c>ITenantOwned</c> dù có cột tiệm: nó thuộc tầng nền tảng, được
/// Superadmin đọc trên toàn hệ thống, và BR-TENANT-022 còn giữ nguyên hóa đơn của tiệm đã
/// xóa mềm để chúng vẫn tính vào doanh thu nền tảng. Gắn bộ lọc theo tiệm đang làm việc
/// vào đây sẽ làm màn hình của Superadmin trống trơn.
/// </para>
/// <para>
/// BR-REV-008 — tổng tiền ở bảng này mới là doanh thu nền tảng mà Superadmin nhìn thấy,
/// không phải doanh thu bán hàng của tiệm.
/// </para>
/// </summary>
public class SubscriptionInvoice
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private SubscriptionInvoice()
    {
        Id = string.Empty;
        Code = string.Empty;
        TenantId = string.Empty;
        TenantName = string.Empty;
        PackageId = string.Empty;
        PackageName = string.Empty;
        Reason = string.Empty;
    }

    private SubscriptionInvoice(
        string id,
        string code,
        string tenantId,
        string tenantName,
        string packageId,
        string packageName,
        long amount,
        BillingCycle billingCycle,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        DateTimeOffset dueAt,
        string reason,
        DateTimeOffset now)
    {
        Id = id;
        Code = code;
        TenantId = tenantId;
        TenantName = tenantName;
        PackageId = packageId;
        PackageName = packageName;
        Amount = amount;
        BillingCycle = billingCycle;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        DueAt = dueAt;
        Reason = reason;
        Status = SubscriptionInvoiceStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Id { get; private set; }
    public string Code { get; private set; }
    public string TenantId { get; private set; }

    /// <summary>
    /// Tên tiệm chép lại tại thời điểm phát hành. BR-INV-031 coi đây là chứng từ tài chính,
    /// nên nó phải đọc được đúng như lúc phát hành kể cả khi tiệm đã đổi tên hoặc đã bị xóa mềm.
    /// </summary>
    public string TenantName { get; private set; }

    public Tenant? Tenant { get; private set; }

    public string PackageId { get; private set; }
    public string PackageName { get; private set; }

    /// <summary>Số tiền, VND (BR-VAL-003 — toàn hệ thống bỏ USD).</summary>
    public long Amount { get; private set; }

    public BillingCycle BillingCycle { get; private set; }
    public DateTimeOffset PeriodStart { get; private set; }
    public DateTimeOffset PeriodEnd { get; private set; }
    public DateTimeOffset DueAt { get; private set; }

    /// <summary>BR-INV-030 — hóa đơn sinh ra từ một trong ba việc: tạo tiệm, nâng gói, gia hạn.</summary>
    public string Reason { get; private set; }

    public SubscriptionInvoiceStatus Status { get; private set; }

    /// <summary>BR-INV-032 — mã giao dịch do chủ tiệm nộp lên, chờ Superadmin xác nhận.</summary>
    public string? PaymentReference { get; private set; }

    public string? PaymentNote { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public string? ConfirmedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static SubscriptionInvoice Issue(
        string id,
        string code,
        string tenantId,
        string tenantName,
        string packageId,
        string packageName,
        long amount,
        BillingCycle billingCycle,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        DateTimeOffset dueAt,
        string reason,
        DateTimeOffset now)
        => new(
            Guard.Reference(id, "id", "Mã hóa đơn đăng ký"),
            Guard.NotEmpty(code, "code", "Số hóa đơn", 40),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            Guard.NotEmpty(tenantName, "tenantName", "Tên tiệm", 200),
            Guard.Reference(packageId, "packageId", "Gói dịch vụ"),
            Guard.NotEmpty(packageName, "packageName", "Tên gói", 80),
            Guard.Money(amount, "amount", "Số tiền"),
            billingCycle,
            periodStart,
            periodEnd,
            dueAt,
            Guard.NotEmpty(reason, "reason", "Lý do phát hành", 80),
            now);

    /// <summary>
    /// BR-INV-032 — chủ tiệm nộp chứng từ thanh toán. BR-TENANT-011 miễn trừ thao tác này
    /// khỏi lệnh chặn ghi, vì nếu chặn luôn thì tiệm hết hạn không còn đường tự mở khóa.
    /// </summary>
    public void SubmitPaymentProof(string reference, string? note, DateTimeOffset now)
    {
        if (Status != SubscriptionInvoiceStatus.Pending)
            throw DomainException.ForField("status", "Chỉ nộp chứng từ cho hóa đơn chưa thanh toán.");

        PaymentReference = Guard.NotEmpty(reference, "paymentReference", "Mã giao dịch", 120);
        PaymentNote = Guard.Optional(note, "paymentNote", "Ghi chú thanh toán", ValidationPolicy.LongTextMaxLength);
        SubmittedAt = now;
        UpdatedAt = now;
    }

    /// <summary>BR-INV-033 — Superadmin xác nhận thì gói được kích hoạt và hạn dùng của tiệm được gia hạn.</summary>
    public void ConfirmPaid(string confirmedByUserId, DateTimeOffset now)
    {
        if (Status == SubscriptionInvoiceStatus.Cancelled)
            throw DomainException.ForField("status", "Hóa đơn đã hủy thì không xác nhận thanh toán được.");

        Status = SubscriptionInvoiceStatus.Paid;
        ConfirmedByUserId = confirmedByUserId;
        PaidAt = now;
        UpdatedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == SubscriptionInvoiceStatus.Paid)
            throw DomainException.ForField("status", "Hóa đơn đã thanh toán thì không hủy được.");

        Status = SubscriptionInvoiceStatus.Cancelled;
        UpdatedAt = now;
    }

    /// <summary>
    /// Quá hạn thanh toán, tính lúc đọc.
    /// <para>
    /// Cố ý không có trạng thái Overdue trong database: BR-TENANT-003 cấm mọi job chạy nền,
    /// nên không có gì để đổi cột đó khi ngày đến hạn trôi qua.
    /// </para>
    /// </summary>
    public bool IsOverdueAt(DateTimeOffset now)
        => Status == SubscriptionInvoiceStatus.Pending && DueAt < now;
}
