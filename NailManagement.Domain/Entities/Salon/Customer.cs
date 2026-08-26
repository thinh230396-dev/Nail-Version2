using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Common;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Policies;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Domain.Entities.Salon;

/// <summary>
/// Khách hàng của tiệm — BR-CUS-001, thuộc về tiệm chứ không thuộc chi nhánh, nên lễ tân
/// ở chi nhánh nào cũng tra cứu được toàn bộ khách.
/// <para>
/// BR-CUS-003 — chỉ số điện thoại là bắt buộc. Tên, email, ngày sinh, ghi chú đều tùy chọn:
/// lúc quầy đông, bắt lễ tân nhập đủ thông tin là cách nhanh nhất để họ bỏ qua phần mềm.
/// </para>
/// <para>
/// Cố ý KHÔNG có cột tổng chi tiêu và số lượt đến. BR-CUS-009 quy định hai số đó tính từ
/// hóa đơn đã thanh toán, và BR-CUS-007 suy hạng khách ra từ chúng lúc đọc.
/// </para>
/// </summary>
public class Customer : ITenantOwned
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private Customer()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        Phone = null!;
    }

    private Customer(
        string id,
        string tenantId,
        PhoneNumber phone,
        string? fullName,
        string? email,
        DateOnly? birthDate,
        string? note,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        Phone = phone;
        FullName = fullName;
        Email = email;
        BirthDate = birthDate;
        Note = note;
        Status = CustomerStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Id { get; private set; }
    public string TenantId { get; private set; }
    public Tenant? Tenant { get; private set; }

    /// <summary>
    /// BR-CUS-002 — duy nhất trong phạm vi MỘT tiệm. Hai tiệm khác nhau được phép có cùng
    /// số điện thoại, vì một người hoàn toàn có thể là khách của cả hai.
    /// </summary>
    public PhoneNumber Phone { get; private set; }

    public string? FullName { get; private set; }
    public string? Email { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public string? Note { get; private set; }
    public CustomerStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Customer Create(
        string id,
        string tenantId,
        string phone,
        string? fullName,
        string? email,
        DateOnly? birthDate,
        string? note,
        DateTimeOffset now)
        => new(
            Guard.Reference(id, "id", "Mã khách hàng"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            PhoneNumber.Create(phone),
            Guard.Optional(fullName, "fullName", "Tên khách hàng", ValidationPolicy.NameMaxLength),
            Guard.Optional(email, "email", "Email khách hàng", 254),
            birthDate,
            Guard.Optional(note, "note", "Ghi chú", ValidationPolicy.LongTextMaxLength),
            now);

    public void UpdateProfile(
        string phone,
        string? fullName,
        string? email,
        DateOnly? birthDate,
        string? note,
        DateTimeOffset now)
    {
        Phone = PhoneNumber.Create(phone);
        FullName = Guard.Optional(fullName, "fullName", "Tên khách hàng", ValidationPolicy.NameMaxLength);
        Email = Guard.Optional(email, "email", "Email khách hàng", 254);
        BirthDate = birthDate;
        Note = Guard.Optional(note, "note", "Ghi chú", ValidationPolicy.LongTextMaxLength);
        UpdatedAt = now;
    }

    /// <summary>BR-CUS-006 — xóa khách là ngừng hoạt động; lịch sử dịch vụ và hóa đơn giữ nguyên.</summary>
    public void Deactivate(DateTimeOffset now)
    {
        Status = CustomerStatus.Inactive;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        Status = CustomerStatus.Active;
        UpdatedAt = now;
    }

    /// <summary>
    /// BR-CUS-007 — hạng khách tính lúc đọc, từ hai con số mà tầng gọi phải tự tổng hợp
    /// được từ hóa đơn. Truyền vào chứ không tự đếm ở đây, vì entity không được biết tới
    /// kho dữ liệu.
    /// </summary>
    public CustomerTier TierFrom(int paidInvoiceCount, long totalSpent)
        => CustomerTierPolicy.Resolve(paidInvoiceCount, totalSpent);
}
