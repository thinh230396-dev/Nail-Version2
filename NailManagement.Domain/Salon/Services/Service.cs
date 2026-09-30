using NailManagement.Domain.Platform.Tenants;
using NailManagement.Domain.Shared;

namespace NailManagement.Domain.Salon.Services;

/// <summary>
/// Dịch vụ tiệm bán — BR-SVC-001, dùng chung cho mọi chi nhánh của tiệm.
/// <para>
/// BR-SVC-008 — không có cấu trúc combo. Một combo chỉ là một bản ghi dịch vụ thông thường
/// có giá riêng, nên ở đây không có dịch vụ con và không có phép tính giá gộp.
/// BR-SVC-009 — không có thuế theo dịch vụ; giá niêm yết đã bao gồm thuế.
/// </para>
/// </summary>
public class Service : ITenantOwned
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private Service()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        Name = string.Empty;
    }

    private Service(
        string id,
        string tenantId,
        string name,
        string? category,
        long price,
        int durationMinutes,
        int bufferMinutes,
        string? description,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Category = category;
        Price = price;
        DurationMinutes = durationMinutes;
        BufferMinutes = bufferMinutes;
        Description = description;
        Status = ServiceStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Id { get; private set; }
    public string TenantId { get; private set; }

    /// <summary>BR-VAL-001 — tên dịch vụ duy nhất trong một tiệm.</summary>
    public string Name { get; private set; }

    public string? Category { get; private set; }

    /// <summary>BR-SVC-002 — mỗi dịch vụ có đúng một giá. Bỏ giá riêng cho khách thành viên.</summary>
    public long Price { get; private set; }

    /// <summary>BR-SVC-003 — thời lượng phục vụ, tính bằng phút.</summary>
    public int DurationMinutes { get; private set; }

    /// <summary>
    /// BR-SVC-003 — thời gian dọn dẹp sau khi làm xong, mặc định 0. Nó cộng vào khoảng
    /// thời gian mà lịch hẹn chiếm chỗ (BR-APT-010) chứ không tính tiền.
    /// </summary>
    public int BufferMinutes { get; private set; }

    public string? Description { get; private set; }
    public ServiceStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Service Create(
        string id,
        string tenantId,
        string name,
        string? category,
        long price,
        int durationMinutes,
        int bufferMinutes,
        string? description,
        DateTimeOffset now)
        => new(
            Guard.Reference(id, "id", "Mã dịch vụ"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            Guard.NotEmpty(name, "name", "Tên dịch vụ", ValidationPolicy.NameMaxLength),
            Guard.Optional(category, "category", "Nhóm dịch vụ", 80),
            Guard.Money(price, "price", "Giá dịch vụ"),
            Guard.Between(durationMinutes,
                ValidationPolicy.ServiceMinDurationMinutes,
                ValidationPolicy.ServiceMaxDurationMinutes,
                "durationMinutes", "Thời lượng dịch vụ (phút)"),
            Guard.Between(bufferMinutes, 0, ValidationPolicy.ServiceMaxBufferMinutes,
                "bufferMinutes", "Thời gian dọn dẹp (phút)"),
            Guard.Optional(description, "description", "Mô tả dịch vụ", ValidationPolicy.LongTextMaxLength),
            now);

    /// <summary>
    /// BR-SVC-006 — đổi giá KHÔNG ảnh hưởng hóa đơn đã lập: dòng hóa đơn giữ giá của
    /// chính nó tại thời điểm lập, không tham chiếu ngược về đây.
    /// </summary>
    public void UpdateDetails(
        string name,
        string? category,
        long price,
        int durationMinutes,
        int bufferMinutes,
        string? description,
        DateTimeOffset now)
    {
        Name = Guard.NotEmpty(name, "name", "Tên dịch vụ", ValidationPolicy.NameMaxLength);
        Category = Guard.Optional(category, "category", "Nhóm dịch vụ", 80);
        Price = Guard.Money(price, "price", "Giá dịch vụ");
        DurationMinutes = Guard.Between(durationMinutes,
            ValidationPolicy.ServiceMinDurationMinutes,
            ValidationPolicy.ServiceMaxDurationMinutes,
            "durationMinutes", "Thời lượng dịch vụ (phút)");
        BufferMinutes = Guard.Between(bufferMinutes, 0, ValidationPolicy.ServiceMaxBufferMinutes,
            "bufferMinutes", "Thời gian dọn dẹp (phút)");
        Description = Guard.Optional(description, "description", "Mô tả dịch vụ", ValidationPolicy.LongTextMaxLength);
        UpdatedAt = now;
    }

    /// <summary>BR-SVC-005 — ngừng bán dịch vụ; lịch hẹn và hóa đơn cũ giữ nguyên.</summary>
    public void Deactivate(DateTimeOffset now)
    {
        Status = ServiceStatus.Inactive;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        Status = ServiceStatus.Active;
        UpdatedAt = now;
    }

    /// <summary>BR-SVC-005 — dịch vụ đã ngừng thì không đặt lịch mới được.</summary>
    public bool IsBookable() => Status == ServiceStatus.Active;
}
