using NailManagement.Domain.Common;
using NailManagement.Domain.Enums.Platform;
using NailManagement.Domain.Policies;

namespace NailManagement.Domain.Entities.Platform;

/// <summary>
/// Yêu cầu nâng cấp hoặc gia hạn gói do chủ tiệm gửi lên — bước đầu trong luồng 5 bước ở
/// BR-SUB-008.
/// <para>
/// BR-SUB-009 — mỗi tiệm chỉ có tối đa một yêu cầu đang chờ; phép kiểm tra đó cần đếm trên
/// bảng nên nằm ở tầng use case, không nằm trong entity này.
/// BR-SUB-010 — yêu cầu không xóa được, chỉ chuyển sang duyệt, từ chối hoặc do chính tiệm hủy.
/// </para>
/// <para>
/// Các cột chép sẵn tên tiệm, tên gói và tên người gửi là cố ý: đây là hồ sơ của một lần
/// thương lượng, phải đọc lại được y nguyên kể cả khi bảng giá đã đổi tên gói về sau.
/// </para>
/// </summary>
public class PackageUpgradeRequest
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private PackageUpgradeRequest()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        TenantName = string.Empty;
        RequestedByName = string.Empty;
        RequestedByEmail = string.Empty;
        CurrentPackageName = string.Empty;
        RequestedPackageId = string.Empty;
        RequestedPackageName = string.Empty;
    }

    private PackageUpgradeRequest(
        string id,
        string tenantId,
        string tenantName,
        string requestedByUserId,
        string requestedByName,
        string requestedByEmail,
        string? currentPackageId,
        string currentPackageName,
        string requestedPackageId,
        string requestedPackageName,
        BillingCycle billingCycle,
        DateTimeOffset effectiveDate,
        long quotedAmount,
        string? note,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        TenantName = tenantName;
        RequestedByUserId = requestedByUserId;
        RequestedByName = requestedByName;
        RequestedByEmail = requestedByEmail;
        CurrentPackageId = currentPackageId;
        CurrentPackageName = currentPackageName;
        RequestedPackageId = requestedPackageId;
        RequestedPackageName = requestedPackageName;
        BillingCycle = billingCycle;
        EffectiveDate = effectiveDate;
        QuotedAmount = quotedAmount;
        Note = note;
        Status = UpgradeRequestStatus.Pending;
        RequestedAt = now;
    }

    public string Id { get; private set; }
    public string TenantId { get; private set; }
    public string TenantName { get; private set; }
    public Tenant? Tenant { get; private set; }

    public string? RequestedByUserId { get; private set; }
    public string RequestedByName { get; private set; }
    public string RequestedByEmail { get; private set; }

    public string? CurrentPackageId { get; private set; }
    public string CurrentPackageName { get; private set; }
    public string RequestedPackageId { get; private set; }
    public string RequestedPackageName { get; private set; }

    public BillingCycle BillingCycle { get; private set; }

    /// <summary>BR-SUB-008 — Superadmin chọn hiệu lực ngay hay từ chu kỳ sau khi duyệt.</summary>
    public DateTimeOffset EffectiveDate { get; private set; }

    /// <summary>Số tiền báo cho tiệm tại thời điểm gửi yêu cầu, VND.</summary>
    public long QuotedAmount { get; private set; }

    public string? Note { get; private set; }
    public UpgradeRequestStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewedByUserId { get; private set; }
    public string? ReviewNote { get; private set; }

    /// <summary>Hóa đơn đăng ký sinh ra khi yêu cầu được duyệt (BR-SUB-008 bước 3).</summary>
    public string? InvoiceId { get; private set; }

    public static PackageUpgradeRequest Submit(
        string id,
        string tenantId,
        string tenantName,
        string requestedByUserId,
        string requestedByName,
        string requestedByEmail,
        string? currentPackageId,
        string currentPackageName,
        string requestedPackageId,
        string requestedPackageName,
        BillingCycle billingCycle,
        DateTimeOffset effectiveDate,
        long quotedAmount,
        string? note,
        DateTimeOffset now)
        => new(
            Guard.Reference(id, "id", "Mã yêu cầu"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            Guard.NotEmpty(tenantName, "tenantName", "Tên tiệm", 200),
            Guard.Reference(requestedByUserId, "requestedByUserId", "Người gửi"),
            Guard.NotEmpty(requestedByName, "requestedByName", "Tên người gửi", ValidationPolicy.NameMaxLength),
            Guard.NotEmpty(requestedByEmail, "requestedByEmail", "Email người gửi", 254),
            string.IsNullOrWhiteSpace(currentPackageId) ? null : currentPackageId.Trim(),
            Guard.NotEmpty(currentPackageName, "currentPackageName", "Gói hiện tại", 80),
            Guard.Reference(requestedPackageId, "requestedPackageId", "Gói muốn nâng"),
            Guard.NotEmpty(requestedPackageName, "requestedPackageName", "Tên gói muốn nâng", 80),
            billingCycle,
            effectiveDate,
            Guard.Money(quotedAmount, "quotedAmount", "Số tiền báo giá"),
            Guard.Optional(note, "note", "Ghi chú yêu cầu", ValidationPolicy.LongTextMaxLength),
            now);

    public void Approve(string reviewedByUserId, string? reviewNote, string? invoiceId, DateTimeOffset now)
    {
        EnsurePending();

        Status = UpgradeRequestStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ReviewNote = Guard.Optional(reviewNote, "reviewNote", "Ghi chú duyệt", ValidationPolicy.LongTextMaxLength);
        InvoiceId = invoiceId;
        ReviewedAt = now;
    }

    public void Reject(string reviewedByUserId, string reviewNote, DateTimeOffset now)
    {
        EnsurePending();

        Status = UpgradeRequestStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        ReviewNote = Guard.NotEmpty(reviewNote, "reviewNote", "Lý do từ chối", ValidationPolicy.LongTextMaxLength);
        ReviewedAt = now;
    }

    /// <summary>BR-TENANT-011 — tiệm tự hủy yêu cầu được, kể cả khi đang bị chặn ghi vì hết hạn.</summary>
    public void CancelByTenant(DateTimeOffset now)
    {
        EnsurePending();

        Status = UpgradeRequestStatus.Cancelled;
        ReviewedAt = now;
    }

    private void EnsurePending()
    {
        if (Status != UpgradeRequestStatus.Pending)
            throw DomainException.ForField("status", $"Yêu cầu đã ở trạng thái {Status}, không xử lý lại được.");
    }
}
