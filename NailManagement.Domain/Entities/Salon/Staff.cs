using NailManagement.Domain.Common;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Policies;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Domain.Entities.Salon;

/// <summary>
/// Hồ sơ nhân viên — BR-EMP-001, bảng chính chứa con người của tiệm.
/// <para>
/// Tài khoản đăng nhập trỏ TỚI bảng này qua <c>AppUser.StaffId</c>, không phải ngược lại.
/// BR-AUTH-002: kỹ thuật viên không có tài khoản đăng nhập, họ chỉ tồn tại ở đây.
/// BR-EMP-004: chi nhánh chỉ lưu trên hồ sơ nhân viên; tài khoản đọc qua hồ sơ chứ không
/// giữ bản sao — hai bản sao là hai chỗ để lệch nhau.
/// </para>
/// </summary>
public class Staff : ITenantOwned, IBranchOwned
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private Staff()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        BranchId = string.Empty;
        FullName = string.Empty;
        SkillsJson = "[]";
    }

    private Staff(
        string id,
        string tenantId,
        string branchId,
        string fullName,
        PhoneNumber? phone,
        string? email,
        StaffRole role,
        TimeOnly shiftStart,
        TimeOnly shiftEnd,
        decimal commissionRate,
        string skillsJson,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        FullName = fullName;
        Phone = phone;
        Email = email;
        Role = role;
        ShiftStart = shiftStart;
        ShiftEnd = shiftEnd;
        CommissionRate = commissionRate;
        SkillsJson = skillsJson;
        Status = StaffStatus.Working;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Id { get; private set; }
    public string TenantId { get; private set; }

    /// <summary>BR-EMP-003 — mỗi nhân viên gắn đúng một chi nhánh; chuyển chi nhánh là sửa trường này.</summary>
    public string BranchId { get; private set; }

    public Branch? Branch { get; private set; }

    public string FullName { get; private set; }
    public PhoneNumber? Phone { get; private set; }
    public string? Email { get; private set; }
    public StaffRole Role { get; private set; }
    public StaffStatus Status { get; private set; }

    /// <summary>
    /// BR-EMP-009 — một ca cố định cho mỗi nhân viên. Không có lịch theo tuần, không có
    /// nghỉ phép, không có chấm công; đó là cả một module riêng đã bị loại khỏi MVP.
    /// </summary>
    public TimeOnly ShiftStart { get; private set; }

    public TimeOnly ShiftEnd { get; private set; }

    /// <summary>
    /// BR-EMP-011 — tỷ lệ hoa hồng, lưu ở đây và NHÂN RA lúc hiển thị báo cáo. Không có
    /// bảng hoa hồng, không chốt kỳ, không duyệt chi.
    /// </summary>
    public decimal CommissionRate { get; private set; }

    /// <summary>BR-EMP-010 — kỹ năng chỉ để hiển thị, hệ thống không cưỡng chế khi phân công.</summary>
    public string SkillsJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Staff Create(
        string id,
        string tenantId,
        string branchId,
        string fullName,
        string? phone,
        string? email,
        StaffRole role,
        TimeOnly shiftStart,
        TimeOnly shiftEnd,
        decimal commissionRate,
        string skillsJson,
        DateTimeOffset now)
    {
        if (shiftEnd <= shiftStart)
            throw DomainException.ForField("shiftEnd", "Giờ kết thúc ca phải sau giờ bắt đầu ca.");

        return new Staff(
            Guard.Reference(id, "id", "Mã nhân viên"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            Guard.Reference(branchId, "branchId", "Chi nhánh"),
            Guard.NotEmpty(fullName, "fullName", "Tên nhân viên", ValidationPolicy.NameMaxLength),
            string.IsNullOrWhiteSpace(phone) ? null : PhoneNumber.Create(phone),
            Guard.Optional(email, "email", "Email nhân viên", 254),
            role,
            shiftStart,
            shiftEnd,
            Guard.Rate(commissionRate, "commissionRate", "Tỷ lệ hoa hồng"),
            skillsJson,
            now);
    }

    public void UpdateProfile(
        string fullName,
        string? phone,
        string? email,
        TimeOnly shiftStart,
        TimeOnly shiftEnd,
        decimal commissionRate,
        string skillsJson,
        DateTimeOffset now)
    {
        if (shiftEnd <= shiftStart)
            throw DomainException.ForField("shiftEnd", "Giờ kết thúc ca phải sau giờ bắt đầu ca.");

        FullName = Guard.NotEmpty(fullName, "fullName", "Tên nhân viên", ValidationPolicy.NameMaxLength);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : PhoneNumber.Create(phone);
        Email = Guard.Optional(email, "email", "Email nhân viên", 254);
        ShiftStart = shiftStart;
        ShiftEnd = shiftEnd;
        CommissionRate = Guard.Rate(commissionRate, "commissionRate", "Tỷ lệ hoa hồng");
        SkillsJson = skillsJson;
        UpdatedAt = now;
    }

    /// <summary>BR-EMP-003 — chuyển chi nhánh.</summary>
    public void MoveToBranch(string branchId, DateTimeOffset now)
    {
        BranchId = Guard.Reference(branchId, "branchId", "Chi nhánh");
        UpdatedAt = now;
    }

    /// <summary>
    /// BR-EMP-002 — đổi vai trò nghiệp vụ giữa kỹ thuật viên và lễ tân.
    /// <para>
    /// Entity cố ý KHÔNG tự chặn gì ở đây, khác với <c>Branch.Deactivate</c>. Điều kiện thật
    /// sự — hồ sơ đang có tài khoản đăng nhập thì không đổi được — nằm ở bảng tài khoản chứ
    /// không nằm trong chính hồ sơ này, nên hồ sơ không có cách nào tự biết. Phép chặn đó
    /// đặt ở <c>UpdateStaffUseCase</c>, chỗ duy nhất nhìn thấy cả hai bảng.
    /// </para>
    /// </summary>
    public void ChangeRole(StaffRole role, DateTimeOffset now)
    {
        Role = role;
        UpdatedAt = now;
    }

    public void ChangeStatus(StaffStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }

    /// <summary>BR-EMP-006 — nghỉ việc là ngừng hoạt động, không xóa bản ghi.</summary>
    public void Deactivate(DateTimeOffset now) => ChangeStatus(StaffStatus.Inactive, now);

    /// <summary>
    /// BR-EMP-007 — nhân viên đã nghỉ việc thì không gán được vào lịch hẹn mới. Ba trạng
    /// thái còn lại vẫn nhận lịch: nghỉ ca hay nghỉ phép là tình trạng tạm thời, lễ tân
    /// vẫn cần đặt trước cho ngày họ đi làm lại.
    /// </summary>
    public bool CanTakeAppointments() => Status != StaffStatus.Inactive;

    /// <summary>BR-EMP-008 — nhân viên chưa nghỉ việc thì tính vào hạn mức của gói.</summary>
    public bool CountsTowardStaffLimit() => Status != StaffStatus.Inactive;
}
