using NailManagement.Domain.Common;
using NailManagement.Domain.Enums;
using NailManagement.Domain.Policies;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Domain.Entities;

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

    // ⚠️ BỐN THUỘC TÍNH DƯỚI LÀ TẠM THỜI — sẽ xóa ở ngày 3.
    // Chúng tồn tại để frontend hiện tại chạy tiếp trong lúc chuyển backend: frontend
    // đang đọc bốn trường này từ GET /api/auth/session (src/auth/demoAccounts.ts).
    // Ngày 3 thay bằng bảng nối UserTenants (BR-AUTH-023) + ActiveTenantId trong phiên
    // (BR-AUTH-024) + chi nhánh đọc qua StaffId (BR-EMP-004).
    public string? TenantId { get; private set; }
    public string? TenantName { get; private set; }
    public string? BranchCode { get; private set; }
    public string? BranchName { get; private set; }

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

    /// <summary>⚠️ Tạm thời — xóa cùng bốn thuộc tính phạm vi ở ngày 3.</summary>
    public void AssignLegacyScope(string? tenantId, string? tenantName, string? branchCode, string? branchName)
    {
        TenantId = tenantId;
        TenantName = tenantName;
        BranchCode = branchCode;
        BranchName = branchName;
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
