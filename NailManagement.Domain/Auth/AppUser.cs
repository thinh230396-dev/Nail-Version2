using NailManagement.Domain.Access;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.Shared;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Domain.Auth;

/// <summary>
/// Tài khoản đăng nhập. Entity này giữ các quy tắc luôn đúng về một tài khoản, bất kể
/// ai đang gọi tới: khi nào thì đăng nhập được, khi nào thì bị khóa.
/// </summary>
public class AppUser
{
    /// <summary>Dành riêng cho EF Core khi dựng lại đối tượng từ database.</summary>
    private AppUser()
    {
        Id = string.Empty;
        Email = null!;
        PasswordHash = string.Empty;
        PasswordSalt = string.Empty;
        DisplayName = string.Empty;
    }

    private AppUser(
        string id,
        Email email,
        string? username,
        string passwordHash,
        string passwordSalt,
        UserRole role,
        string displayName,
        DateTimeOffset now)
    {
        Id = id;
        Email = email;
        Username = username;
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        Role = role;
        DisplayName = displayName;
        Status = AccountStatus.Active;
        FailedAttempts = 0;
        LockedUntil = null;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Id { get; private set; }
    public Email Email { get; private set; }
    public string? Username { get; private set; }
    public string PasswordHash { get; private set; }
    public string PasswordSalt { get; private set; }
    public UserRole Role { get; private set; }
    public string DisplayName { get; private set; }
    public AccountStatus Status { get; private set; }
    public int FailedAttempts { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>
    /// BR-AUTH-013/014 — chỉ tài khoản lễ tân mới trỏ tới một hồ sơ nhân viên.
    /// SuperAdmin và TenantAdmin luôn null, do đó không thuộc chi nhánh nào.
    /// Khóa ngoại sang bảng <c>Staff</c> đã được nối ở ngày 2.
    /// </summary>
    public string? StaffId { get; private set; }

    // Bốn cột tạm TenantId / TenantName / BranchCode / BranchName đã được gỡ ở ngày 4.
    // Chúng từng chép sẵn phạm vi làm việc lên chính bảng tài khoản để frontend cũ chạy
    // tiếp trong lúc chuyển backend. Nay ba nguồn thật đã thay thế đủ:
    //   · tiệm quản lý được   → bảng UserTenants          (BR-AUTH-023)
    //   · tiệm đang làm việc  → ActiveTenantId của phiên  (BR-AUTH-024)
    //   · chi nhánh           → đọc qua StaffId           (BR-EMP-004)

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static AppUser Create(
        string id,
        Email email,
        string? username,
        string passwordHash,
        string passwordSalt,
        UserRole role,
        string displayName,
        DateTimeOffset now)
        => new(id, email, username, passwordHash, passwordSalt, role, displayName, now);

    /// <summary>
    /// BR-AUTH-013 — gắn tài khoản vào một hồ sơ nhân viên đã tồn tại. Đây là bước "Cấp
    /// tài khoản đăng nhập" bấm trên hồ sơ lễ tân, và cũng là đường DUY NHẤT để một tài
    /// khoản có chi nhánh: BR-EMP-004 quy định chi nhánh chỉ nằm trên hồ sơ nhân viên.
    /// </summary>
    public void AttachStaff(string staffId, DateTimeOffset now)
    {
        if (Role != UserRole.Receptionist)
            throw DomainException.ForField("role", "Chỉ tài khoản lễ tân mới gắn được với hồ sơ nhân viên.");

        StaffId = Guard.Reference(staffId, "staffId", "Hồ sơ nhân viên");
        UpdatedAt = now;
    }

    /// <summary>
    /// BR-AUTH-021 — chỉ tài khoản Active mới đăng nhập được.
    /// BR-AUTH-022 — phép kiểm tra này còn được gọi lại ở MỖI lần đọc phiên, không chỉ
    /// lúc đăng nhập, nên tài khoản bị khóa mất quyền ngay ở request kế tiếp.
    /// </summary>
    public bool IsActive() => Status == AccountStatus.Active;

    /// <summary>Đang trong thời gian khóa tạm vì nhập sai quá nhiều lần.</summary>
    public bool IsLockedAt(DateTimeOffset now) => LockedUntil is not null && LockedUntil > now;

    /// <summary>
    /// BR-AUTH-020 / BR-DEL-001 — "xóa tài khoản" là vô hiệu hóa vĩnh viễn, không xóa bản ghi.
    /// <para>
    /// Đường gọi duy nhất hiện nay là lúc hồ sơ nhân viên chuyển sang nghỉ việc: BR-EMP-006
    /// giữ lại hồ sơ để tên người đó vẫn hiện đúng trong lịch hẹn và hóa đơn cũ, nhưng một
    /// lễ tân đã nghỉ mà vẫn đăng nhập được vào quầy thu tiền thì là lỗ hổng. Nhờ BR-AUTH-022,
    /// phiên đang mở của họ chết ngay ở request kế tiếp chứ không đợi hết hạn.
    /// </para>
    /// <para>
    /// Cố ý KHÔNG có hàm ngược lại. Bật lại hồ sơ nhân viên là chuyện nhân sự; cấp lại quyền
    /// đăng nhập là một quyết định riêng và phải được bấm riêng.
    /// </para>
    /// </summary>
    public void Deactivate(DateTimeOffset now)
    {
        Status = AccountStatus.Inactive;
        UpdatedAt = now;
    }

    /// <summary>
    /// BR-AUTH-020 — khóa tạm một tài khoản. Mở lại được bằng <see cref="Restore"/>.
    /// <para>
    /// Khác <see cref="Deactivate"/> ở chỗ căn bản, và đó là lý do hai hàm không gộp làm một:
    /// vô hiệu là <b>vĩnh viễn</b> và dành cho người đã rời khỏi tiệm, còn khóa là biện pháp
    /// tạm thời với một người vẫn đang làm — nợ phí, nghi ngờ lộ mật khẩu, đang điều tra một
    /// sự việc. Trộn chúng vào một trạng thái là biến một quyết định tạm thời thành không thể
    /// hoàn tác, trong khi BR-DEL-001 lại không cho xóa để tạo lại.
    /// </para>
    /// <para>
    /// Không cần thêm gì để cưỡng chế: <see cref="IsActive"/> chỉ đúng khi trạng thái là
    /// <c>Active</c>, và BR-AUTH-022 đọc lại tài khoản ở mỗi request — nên người bị khóa mất
    /// quyền ngay ở request kế tiếp, đúng như khi bị vô hiệu.
    /// </para>
    /// <para>
    /// Đặt <c>LockedUntil = null</c> là cố ý. Cột ấy thuộc về phép khóa tạm <b>tự động</b> sau
    /// năm lần nhập sai, và để nguyên thì một tài khoản vừa bị khóa tay vừa đang đếm ngược một
    /// khóa tự động — hết giờ đếm ngược, cột kia hết hiệu lực, và người đọc database không biết
    /// tài khoản này còn bị khóa hay không. Một lý do khóa tại một thời điểm.
    /// </para>
    /// </summary>
    public void Suspend(DateTimeOffset now)
    {
        Status = AccountStatus.Suspended;
        LockedUntil = null;
        FailedAttempts = 0;
        UpdatedAt = now;
    }

    /// <summary>
    /// Mở khóa một tài khoản đang bị khóa tạm — BR-AUTH-020.
    /// <para>
    /// Chỉ đưa <c>Suspended</c> về <c>Active</c>. Tài khoản đã <c>Inactive</c> KHÔNG mở lại
    /// được bằng đường này: vô hiệu là vĩnh viễn theo đúng chú thích ở <see cref="Deactivate"/>,
    /// và cho phép hoàn tác ở đây là lặng lẽ gỡ bỏ tính vĩnh viễn ấy. Phép kiểm nằm ở tầng use
    /// case vì nó cần câu chữ để nói cho người dùng biết vì sao không mở được.
    /// </para>
    /// </summary>
    public void Restore(DateTimeOffset now)
    {
        Status = AccountStatus.Active;
        LockedUntil = null;
        FailedAttempts = 0;
        UpdatedAt = now;
    }

    /// <summary>
    /// Ghi nhận một lần nhập sai. Chạm ngưỡng thì khóa tạm và đặt lại bộ đếm về 0, để sau
    /// khi hết hạn khóa người dùng lại có đủ số lần thử.
    /// </summary>
    public void RegisterFailedAttempt(DateTimeOffset now)
    {
        var attempts = FailedAttempts + 1;
        var reachedLimit = attempts >= AuthPolicy.MaxFailedAttempts;

        FailedAttempts = reachedLimit ? 0 : attempts;
        LockedUntil = reachedLimit ? now.Add(AuthPolicy.LockDuration) : null;
        UpdatedAt = now;
    }

    /// <summary>Đăng nhập thành công thì xóa sạch dấu vết của các lần sai trước đó.</summary>
    public void RegisterSuccessfulLogin(DateTimeOffset now)
    {
        FailedAttempts = 0;
        LockedUntil = null;
        UpdatedAt = now;
    }
}
