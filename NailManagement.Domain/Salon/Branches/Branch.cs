using NailManagement.Domain.Platform.Tenants;
using NailManagement.Domain.Shared;

namespace NailManagement.Domain.Salon.Branches;

/// <summary>
/// Chi nhánh của một tiệm.
/// <para>
/// BR-BRANCH-006/007 — ranh giới dữ liệu: khách hàng và dịch vụ dùng chung toàn tiệm, còn
/// lịch hẹn, nhân viên và hóa đơn bán hàng thuộc riêng từng chi nhánh. Lễ tân chỉ thao tác
/// được với nhóm sau của chi nhánh mình (BR-ISO-004).
/// </para>
/// </summary>
public class Branch : ITenantOwned
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private Branch()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        Name = string.Empty;
    }

    private Branch(
        string id,
        string tenantId,
        string name,
        string? code,
        string? address,
        string? phone,
        bool isPrimary,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Code = code;
        Address = address;
        Phone = phone;
        IsPrimary = isPrimary;
        Status = BranchStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Id { get; private set; }
    public string TenantId { get; private set; }

    /// <summary>BR-VAL-001 — tên chi nhánh duy nhất trong một tiệm, 3–80 ký tự.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// Mã ngắn để lễ tân gọi tên nhanh, ví dụ Q1 hay Q3. Giao diện hiện tại đang gắn cứng
    /// hai mã này ở bảy tệp; ngày 6 của lộ trình sẽ gỡ chúng và đọc từ đây.
    /// </summary>
    public string? Code { get; private set; }

    public string? Address { get; private set; }
    public string? Phone { get; private set; }

    /// <summary>
    /// BR-BRANCH-001 — mỗi tiệm có đúng một chi nhánh chính, tạo tự động cùng tiệm
    /// (BR-TENANT-005), nên tiệm không bao giờ ở trạng thái không có chi nhánh nào.
    /// </summary>
    public bool IsPrimary { get; private set; }

    public BranchStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Branch Create(
        string id,
        string tenantId,
        string name,
        string? code,
        string? address,
        string? phone,
        bool isPrimary,
        DateTimeOffset now)
        => new(
            Guard.Reference(id, "id", "Mã chi nhánh"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            Guard.Length(name, "name", "Tên chi nhánh",
                ValidationPolicy.BranchNameMinLength, ValidationPolicy.BranchNameMaxLength),
            Guard.Optional(code, "code", "Mã ngắn chi nhánh", 32),
            Guard.Optional(address, "address", "Địa chỉ chi nhánh", 300),
            Guard.Optional(phone, "phone", "Số điện thoại chi nhánh", 20),
            isPrimary,
            now);

    public void UpdateProfile(string name, string? code, string? address, string? phone, DateTimeOffset now)
    {
        Name = Guard.Length(name, "name", "Tên chi nhánh",
            ValidationPolicy.BranchNameMinLength, ValidationPolicy.BranchNameMaxLength);
        Code = Guard.Optional(code, "code", "Mã ngắn chi nhánh", 32);
        Address = Guard.Optional(address, "address", "Địa chỉ chi nhánh", 300);
        Phone = Guard.Optional(phone, "phone", "Số điện thoại chi nhánh", 20);
        UpdatedAt = now;
    }

    /// <summary>
    /// BR-BRANCH-002/004 — xóa chi nhánh thực chất là ngừng hoạt động, và chi nhánh chính
    /// thì không được phép ngừng. Quy tắc này nằm ở entity chứ không ở use case vì nó luôn
    /// đúng, bất kể ai gọi tới và gọi từ màn hình nào.
    /// </summary>
    public void Deactivate(DateTimeOffset now)
    {
        if (IsPrimary)
            throw DomainException.ForField("status", "Không thể ngừng hoạt động chi nhánh chính của tiệm.");

        Status = BranchStatus.Inactive;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        Status = BranchStatus.Active;
        UpdatedAt = now;
    }

    /// <summary>BR-BRANCH-004 — chi nhánh đã ngừng thì không nhận lịch hẹn mới.</summary>
    public bool AcceptsNewAppointments() => Status == BranchStatus.Active;
}
