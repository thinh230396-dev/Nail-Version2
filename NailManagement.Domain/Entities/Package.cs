using NailManagement.Domain.Common;
using NailManagement.Domain.Enums;

namespace NailManagement.Domain.Entities;

/// <summary>
/// Gói dịch vụ mà SalonSys bán cho tiệm.
/// <para>
/// Hai hạn mức <see cref="MaxSalons"/> và <see cref="MaxStaff"/> là hạn mức DUY NHẤT được
/// cưỡng chế thật (BR-SUB-005). Các hạn mức còn lại — số lịch hẹn mỗi tháng, dung lượng,
/// số tin nhắn — nằm trong <see cref="LimitsJson"/> và chỉ để hiển thị trên bảng giá
/// (BR-SUB-006), vì MVP không có gì để đo chúng.
/// </para>
/// </summary>
public class Package
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private Package()
    {
        Id = string.Empty;
        Name = string.Empty;
        CapabilitiesJson = "[]";
        FeaturesJson = "[]";
        LimitsJson = "{}";
    }

    private Package(
        string id,
        string name,
        string? description,
        long price,
        BillingCycle billingCycle,
        int maxSalons,
        int maxStaff,
        string capabilitiesJson,
        string featuresJson,
        string limitsJson,
        string? color,
        DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        BillingCycle = billingCycle;
        MaxSalons = maxSalons;
        MaxStaff = maxStaff;
        CapabilitiesJson = capabilitiesJson;
        FeaturesJson = featuresJson;
        LimitsJson = limitsJson;
        Color = color;
        Status = PackageStatus.Active;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Id { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>Giá gói, VND số nguyên (BR-VAL-003 — toàn hệ thống bỏ USD).</summary>
    public long Price { get; private set; }

    public BillingCycle BillingCycle { get; private set; }

    /// <summary>BR-BRANCH-005 — số chi nhánh đang hoạt động của tiệm không được vượt con số này.</summary>
    public int MaxSalons { get; private set; }

    /// <summary>BR-EMP-008 — số nhân viên chưa nghỉ việc của tiệm không được vượt con số này.</summary>
    public int MaxStaff { get; private set; }

    /// <summary>
    /// BR-SUB-007 — quyền tính năng theo gói, chép từ bảng ánh xạ có sẵn ở frontend
    /// (<c>src/utils/subscriptions.ts</c>). Lưu một cột JSON thay vì một bảng nối vì danh
    /// sách này chỉ được đọc nguyên khối, chưa bao giờ cần truy vấn theo từng quyền.
    /// </summary>
    public string CapabilitiesJson { get; private set; }

    /// <summary>Các dòng mô tả hiển thị trên bảng giá. Không mang ý nghĩa cưỡng chế.</summary>
    public string FeaturesJson { get; private set; }

    /// <summary>BR-SUB-006 — sáu hạn mức chỉ để trưng bày, không đo lường, không cưỡng chế.</summary>
    public string LimitsJson { get; private set; }

    public string? Color { get; private set; }
    public PackageStatus Status { get; private set; }

    /// <summary>
    /// BR-SUB-004 — giá gói được khóa theo phiên bản tại thời điểm tiệm đăng ký. Mỗi lần
    /// đổi bảng giá thì số này tăng, còn tiệm đang dùng vẫn giữ phiên bản cũ của mình.
    /// </summary>
    public int Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Package Create(
        string id,
        string name,
        string? description,
        long price,
        BillingCycle billingCycle,
        int maxSalons,
        int maxStaff,
        string capabilitiesJson,
        string featuresJson,
        string limitsJson,
        string? color,
        DateTimeOffset now)
        => new(
            Guard.Reference(id, "id", "Mã gói"),
            Guard.NotEmpty(name, "name", "Tên gói", 80),
            Guard.Optional(description, "description", "Mô tả gói", 500),
            Guard.Money(price, "price", "Giá gói"),
            billingCycle,
            Guard.Between(maxSalons, 1, 999, "maxSalons", "Số chi nhánh tối đa"),
            Guard.Between(maxStaff, 1, 99999, "maxStaff", "Số nhân viên tối đa"),
            capabilitiesJson,
            featuresJson,
            limitsJson,
            Guard.Optional(color, "color", "Màu nhãn gói", 20),
            now);

    /// <summary>Đổi bảng giá: tăng số phiên bản để tiệm đang dùng giữ nguyên giá cũ (BR-SUB-004).</summary>
    public void Reprice(long price, DateTimeOffset now)
    {
        Price = Guard.Money(price, "price", "Giá gói");
        Version += 1;
        UpdatedAt = now;
    }

    public void ChangeStatus(PackageStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }

    /// <summary>BR-SUB-002 — chỉ gói đang bán mới nhận đăng ký mới.</summary>
    public bool AcceptsNewSubscriptions() => Status == PackageStatus.Active;
}
