namespace NailManagement.Application.Features.Tenants;

/// <summary>
/// Một tiệm trong màn chọn tiệm — BR-AUTH-025.
/// <para>
/// <paramref name="DisplayStatus"/> là kết quả TÍNH lúc đọc theo BR-TENANT-002, không phải
/// cột trong database. Nhờ vậy tiệm vừa quá hạn hiện đúng trạng thái ngay lần bấm kế tiếp
/// mà hệ thống không cần một tiến trình chạy nền nào.
/// </para>
/// </summary>
public sealed record TenantSummaryDto(
    string Id,
    string Code,
    string Name,
    string DisplayStatus,
    bool IsReadOnly,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Chi nhánh mà tài khoản lễ tân làm việc — BR-ISO-004.
/// <para>
/// BR-EMP-004 — chi nhánh chỉ lưu trên hồ sơ nhân viên, tài khoản đọc qua đó chứ không giữ
/// bản sao. Cho tới ngày 3 bảng tài khoản còn hai cột tạm <c>BranchCode</c> và
/// <c>BranchName</c> chép sẵn thông tin này; ngày 4 đã gỡ, và đây là thứ thay thế.
/// </para>
/// <para>
/// Rỗng với Superadmin và chủ tiệm: BR-AUTH-014 quy định hai vai trò đó không gắn hồ sơ
/// nhân viên, nên không thuộc chi nhánh nào.
/// </para>
/// </summary>
public sealed record BranchScopeDto(string Id, string? Code, string Name);

/// <summary>
/// Tiệm đang làm việc của phiên, kèm những thứ mà tầng phân quyền cần ở mỗi request.
/// <para>
/// <paramref name="Capabilities"/> là danh sách quyền mà gói của tiệm mở (BR-SUB-007). Nó
/// đi kèm ở đây thay vì để tầng trên tự đi đọc bảng gói, vì bước 2 của BR-TENANT-013 chạy
/// trên MỌI request — thêm một lượt truy vấn nữa là thêm một lượt cho mỗi lần bấm chuột.
/// </para>
/// </summary>
public sealed record TenantScopeDto(
    string Id,
    string Code,
    string Name,
    string DisplayStatus,
    bool IsReadOnly,
    string PackageId,
    string PackageName,
    IReadOnlyList<string> Capabilities);

/// <summary>Tài khoản chủ tiệm được giao quản lý một tiệm — BR-AUTH-023.</summary>
public sealed record TenantOwnerDto(
    string Id,
    string Email,
    string? Username,
    string DisplayName,
    string Status);

/// <summary>
/// Một tiệm trong màn quản lý tiệm của Superadmin.
/// <para>
/// <paramref name="ActiveBranches"/> và <paramref name="ActiveStaff"/> là hai con số duy
/// nhất đến từ bên trong tiệm, và chúng có mặt vì BR-SUB-005: đây là hai hạn mức được
/// cưỡng chế thật, nên người bán gói phải thấy tiệm nào sắp chạm trần. Doanh thu của tiệm
/// cố ý KHÔNG có ở đây — BR-AUTH-030 xếp nó vào dữ liệu nghiệp vụ mà Superadmin không được
/// truy cập. Doanh thu mà Superadmin nhìn thấy là doanh thu nền tảng ở BR-REV-008, tính từ
/// hóa đơn đăng ký chứ không phải từ hóa đơn bán hàng của tiệm.
/// </para>
/// <para>
/// <paramref name="DaysRemaining"/> tính lúc đọc như <paramref name="DisplayStatus"/>, và
/// mang giá trị âm khi tiệm đã quá hạn — frontend đang hiển thị đúng như vậy.
/// </para>
/// </summary>
public sealed record TenantDetailDto(
    string Id,
    string Code,
    string Name,
    string DisplayStatus,
    bool IsReadOnly,
    bool IsTrial,
    DateTimeOffset ExpiresAt,
    int DaysRemaining,
    string? Address,
    string? Phone,
    string? ContactEmail,
    string Timezone,
    string PackageId,
    string PackageName,
    long SubscriptionPrice,
    int SubscriptionPackageVersion,
    string BillingCycle,
    DateTimeOffset SubscriptionStartedAt,
    int MaxSalons,
    int MaxStaff,
    int ActiveBranches,
    int ActiveStaff,
    IReadOnlyList<TenantOwnerDto> Owners,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Chủ tiệm cho một tiệm mới — BR-TENANT-004, Superadmin chọn một trong hai đường.
/// </summary>
/// <param name="Mode">
/// <c>new</c> tạo tài khoản mới, <c>existing</c> gán một tài khoản chủ tiệm đã có. Là chuỗi
/// chứ không phải enum vì nó đến thẳng từ ô chọn trên giao diện; use case tự kiểm tra và
/// trả lỗi gắn đúng ô nhập nếu sai.
/// </param>
/// <param name="Password">
/// Bỏ trống thì máy chủ tự sinh và trả lại đúng một lần trong
/// <see cref="CreateTenantResult.GeneratedPassword"/>. Hệ thống không gửi email nên nếu
/// không trả về thì tài khoản vừa tạo không ai đăng nhập được.
/// </param>
public sealed record TenantOwnerCommand(
    string Mode,
    string? ExistingUserId,
    string? Email,
    string? Username,
    string? DisplayName,
    string? Password);

/// <summary>Tạo tiệm — BR-TENANT-004/005, chạy trọn trong một giao dịch.</summary>
public sealed record CreateTenantCommand(
    string Code,
    string Name,
    string PackageId,
    DateTimeOffset ExpiresAt,
    bool IsTrial,
    string? BillingCycle,
    string? Address,
    string? Phone,
    string? ContactEmail,
    string? Timezone,
    string? PrimaryBranchName,
    string? PrimaryBranchCode,
    TenantOwnerCommand Owner);

/// <param name="GeneratedPassword">
/// Chỉ khác null khi máy chủ vừa tự sinh mật khẩu cho một tài khoản chủ tiệm mới. Đây là
/// lần duy nhất chuỗi đó tồn tại ở dạng đọc được — sau khi response rời khỏi máy chủ,
/// database chỉ còn bản băm PBKDF2.
/// </param>
public sealed record CreateTenantResult(TenantDetailDto Tenant, string? GeneratedPassword);

public sealed record UpdateTenantCommand(
    string TenantId,
    string Name,
    string? Address,
    string? Phone,
    string? ContactEmail);

/// <summary>BR-TENANT-006 — Superadmin nhập tay hạn dùng mới.</summary>
public sealed record RenewTenantCommand(string TenantId, DateTimeOffset ExpiresAt);

/// <param name="Status">
/// <c>ACTIVE</c> hoặc <c>SUSPENDED</c> — đúng hai trạng thái mà database lưu (BR-TENANT-002).
/// <c>TRIAL</c> và <c>OVERDUE</c> không đặt tay được vì chúng là kết quả tính lúc đọc.
/// </param>
public sealed record ChangeTenantStatusCommand(string TenantId, string Status);
