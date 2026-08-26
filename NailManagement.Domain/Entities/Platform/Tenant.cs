using System.Text.RegularExpressions;
using NailManagement.Domain.Common;
using NailManagement.Domain.Enums.Platform;

namespace NailManagement.Domain.Entities.Platform;

/// <summary>
/// Một tiệm nail đăng ký dùng SalonSys. Đây là gốc của mọi dữ liệu nghiệp vụ: xóa nhầm
/// hay lộ nhầm một tenant là lộ toàn bộ dữ liệu của một khách hàng doanh nghiệp.
/// </summary>
public partial class Tenant
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private Tenant()
    {
        Id = string.Empty;
        Code = string.Empty;
        Name = string.Empty;
        PackageId = string.Empty;
        Timezone = string.Empty;
    }

    private Tenant(
        string id,
        string code,
        string name,
        string packageId,
        long subscriptionPrice,
        int packageVersion,
        BillingCycle billingCycle,
        bool isTrial,
        DateTimeOffset expiresAt,
        string? address,
        string? phone,
        string? contactEmail,
        string timezone,
        DateTimeOffset now)
    {
        Id = id;
        Code = code;
        Name = name;
        PackageId = packageId;
        SubscriptionPrice = subscriptionPrice;
        SubscriptionPackageVersion = packageVersion;
        BillingCycle = billingCycle;
        IsTrial = isTrial;
        ExpiresAt = expiresAt;
        Address = address;
        Phone = phone;
        ContactEmail = contactEmail;
        Timezone = timezone;
        Status = TenantStatus.Active;
        SubscriptionStartedAt = now;
        DeletedAt = null;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Id { get; private set; }

    /// <summary>BR-VAL-001 — mã tiệm duy nhất toàn hệ thống, chữ in hoa và chữ số.</summary>
    public string Code { get; private set; }

    public string Name { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? ContactEmail { get; private set; }
    public string Timezone { get; private set; }

    /// <summary>
    /// BR-TENANT-002 — cột này chỉ nhận Active hoặc Suspended. Hai trạng thái còn lại mà
    /// người dùng nhìn thấy được tính ra ở <see cref="DisplayStatusAt"/>.
    /// </summary>
    public TenantStatus Status { get; private set; }

    /// <summary>BR-SUB-011 — tiệm đang dùng thử. Hết hạn thì xử lý y hệt hết hạn thường.</summary>
    public bool IsTrial { get; private set; }

    /// <summary>BR-TENANT-006 — Superadmin nhập tay ngày này khi tạo và khi gia hạn.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    public string PackageId { get; private set; }
    public Package? Package { get; private set; }

    /// <summary>
    /// BR-SUB-004 — giá và số phiên bản gói được chốt tại thời điểm đăng ký. Về sau
    /// Superadmin có sửa bảng giá thì tiệm này vẫn trả đúng số đã thỏa thuận.
    /// </summary>
    public long SubscriptionPrice { get; private set; }

    public int SubscriptionPackageVersion { get; private set; }
    public BillingCycle BillingCycle { get; private set; }
    public DateTimeOffset SubscriptionStartedAt { get; private set; }

    /// <summary>BR-TENANT-020 — xóa tiệm là xóa mềm, dữ liệu vẫn ở lại database.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Tenant Create(
        string id,
        string code,
        string name,
        string packageId,
        long subscriptionPrice,
        int packageVersion,
        BillingCycle billingCycle,
        bool isTrial,
        DateTimeOffset expiresAt,
        string? address,
        string? phone,
        string? contactEmail,
        DateTimeOffset now,
        string timezone = "Asia/Ho_Chi_Minh")
    {
        var normalizedCode = Guard.NotEmpty(code, "code", "Mã tiệm", 32).ToUpperInvariant();

        if (!CodePattern().IsMatch(normalizedCode))
            throw DomainException.ForField("code", "Mã tiệm chỉ gồm chữ in hoa, chữ số và dấu gạch ngang.");

        // BR-VAL-001 — hạn dùng bắt buộc và phải sau ngày bắt đầu. Thiếu phép kiểm tra này
        // thì tạo xong tiệm đã ở trạng thái quá hạn, và không ai hiểu vì sao.
        if (expiresAt <= now)
            throw DomainException.ForField("expiresAt", "Hạn sử dụng phải sau ngày bắt đầu.");

        return new Tenant(
            Guard.Reference(id, "id", "Mã định danh tiệm"),
            normalizedCode,
            Guard.NotEmpty(name, "name", "Tên tiệm", 200),
            Guard.Reference(packageId, "packageId", "Gói dịch vụ"),
            Guard.Money(subscriptionPrice, "subscriptionPrice", "Giá gói đã chốt"),
            packageVersion,
            billingCycle,
            isTrial,
            expiresAt,
            Guard.Optional(address, "address", "Địa chỉ", 300),
            Guard.Optional(phone, "phone", "Số điện thoại", 20),
            Guard.Optional(contactEmail, "contactEmail", "Email liên hệ", 254),
            timezone,
            now);
    }

    /// <summary>
    /// BR-TENANT-002 — trạng thái người dùng nhìn thấy, tính lúc đọc.
    /// <para>
    /// Thứ tự các nhánh ở đây là một phần của quy tắc: tiệm bị khóa tay thì hiển thị
    /// Suspended kể cả khi còn hạn, và tiệm dùng thử đã quá hạn thì hiển thị Overdue chứ
    /// không phải Trial — nếu không thì tiệm hết hạn dùng thử trông như vẫn bình thường.
    /// </para>
    /// </summary>
    public TenantDisplayStatus DisplayStatusAt(DateTimeOffset now)
    {
        if (Status == TenantStatus.Suspended) return TenantDisplayStatus.Suspended;
        if (ExpiresAt < now) return TenantDisplayStatus.Overdue;
        if (IsTrial) return TenantDisplayStatus.Trial;

        return TenantDisplayStatus.Active;
    }

    /// <summary>
    /// BR-TENANT-010 — tiệm quá hạn hoặc bị khóa vẫn đăng nhập và xem được mọi thứ, nhưng
    /// mọi thao tác ghi bị chặn. Đây là hàm mà middleware chặn ghi ở ngày 3 sẽ gọi
    /// (BR-TENANT-012); BR-TENANT-007 quy định không có thời gian ân hạn nào.
    /// </summary>
    public bool IsReadOnlyAt(DateTimeOffset now)
        => DisplayStatusAt(now) is TenantDisplayStatus.Overdue or TenantDisplayStatus.Suspended;

    public void UpdateProfile(string name, string? address, string? phone, string? contactEmail, DateTimeOffset now)
    {
        Name = Guard.NotEmpty(name, "name", "Tên tiệm", 200);
        Address = Guard.Optional(address, "address", "Địa chỉ", 300);
        Phone = Guard.Optional(phone, "phone", "Số điện thoại", 20);
        ContactEmail = Guard.Optional(contactEmail, "contactEmail", "Email liên hệ", 254);
        UpdatedAt = now;
    }

    /// <summary>BR-TENANT-006 — gia hạn là đẩy hạn dùng ra xa, do Superadmin nhập tay.</summary>
    public void Renew(DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (expiresAt <= now)
            throw DomainException.ForField("expiresAt", "Hạn sử dụng mới phải ở tương lai.");

        ExpiresAt = expiresAt;
        IsTrial = false;
        UpdatedAt = now;
    }

    /// <summary>BR-INV-033 — hóa đơn đăng ký được xác nhận thì gói mới có hiệu lực.</summary>
    public void ApplyPackage(string packageId, long price, int packageVersion, BillingCycle billingCycle, DateTimeOffset now)
    {
        PackageId = Guard.Reference(packageId, "packageId", "Gói dịch vụ");
        SubscriptionPrice = Guard.Money(price, "subscriptionPrice", "Giá gói đã chốt");
        SubscriptionPackageVersion = packageVersion;
        BillingCycle = billingCycle;
        SubscriptionStartedAt = now;
        UpdatedAt = now;
    }

    public void Suspend(DateTimeOffset now)
    {
        Status = TenantStatus.Suspended;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        Status = TenantStatus.Active;
        UpdatedAt = now;
    }

    /// <summary>
    /// BR-TENANT-020 — xóa mềm. Không có hàm xóa cứng nào trong toàn hệ thống (BR-DEL-001),
    /// và BR-TENANT-022 giữ nguyên hóa đơn đăng ký của tiệm đã xóa.
    /// </summary>
    public void SoftDelete(DateTimeOffset now)
    {
        DeletedAt = now;
        UpdatedAt = now;
    }

    public bool IsDeleted => DeletedAt is not null;

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9-]*$")]
    private static partial Regex CodePattern();
}
