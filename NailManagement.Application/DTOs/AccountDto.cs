namespace NailManagement.Application.DTOs;

/// <summary>
/// Hình dạng tài khoản mà frontend nhận được.
/// <para>
/// Chỉ còn <b>danh tính</b> của người đăng nhập: họ là ai và vai trò gì. Phạm vi làm việc —
/// tiệm nào, chi nhánh nào — đã tách sang <see cref="TenantScopeDto"/> và
/// <see cref="BranchScopeDto"/> ở ngày 4, vì hai thứ đó thuộc về PHIÊN chứ không thuộc về
/// tài khoản: cùng một tài khoản chủ tiệm có thể đang làm việc cho tiệm này hay tiệm khác
/// tùy lúc (BR-AUTH-023, BR-AUTH-024).
/// </para>
/// <para>
/// DTO cố ý <b>không</b> phải entity <c>AppUser</c>: entity có <c>PasswordHash</c>,
/// <c>PasswordSalt</c>, <c>FailedAttempts</c> — những thứ không bao giờ được ra khỏi máy
/// chủ. Tách DTO là hàng rào ngăn chuyện đó xảy ra do vô ý.
/// </para>
/// </summary>
public sealed record AccountDto
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required string DisplayName { get; init; }
}

/// <summary>
/// Một tài khoản chủ tiệm trong màn quản lý tài khoản của Superadmin, và trong ô chọn chủ
/// tiệm ở form tạo tiệm (BR-TENANT-004, <c>owner.mode = "existing"</c>).
/// <para>
/// Khác <see cref="AccountDto"/> ở chỗ nó mang thêm <paramref name="TenantIds"/> — thứ mà
/// DTO phiên đăng nhập cố ý không có, vì phiên chỉ quan tâm tới <b>một</b> tiệm đang làm
/// việc. Ở đây thì ngược lại: câu hỏi chính là "người này đang giữ mấy tiệm".
/// </para>
/// <para>
/// Trả về mã tiệm chứ không trả tên tiệm. Màn hình nào cần tên thì đã có sẵn danh sách tiệm
/// từ <c>GET /api/tenants</c> để ghép — gửi kèm tên ở đây là chép cùng một sự thật ra hai
/// chỗ, rồi có ngày hai chỗ lệch nhau sau một lần đổi tên tiệm.
/// </para>
/// </summary>
public sealed record TenantAdminAccountDto(
    string Id,
    string Email,
    string? Username,
    string DisplayName,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> TenantIds);

/// <summary>
/// Lệnh khóa hoặc mở khóa một tài khoản chủ tiệm — BR-AUTH-020.
/// <para>
/// Cùng hình dạng với <c>ChangeTenantStatusCommand</c>, và giống nhau là cố ý: hai màn hình
/// nằm cạnh nhau trong cổng Superadmin, nên hai lệnh nên đọc lên như nhau. <c>Status</c> nhận
/// chuỗi thô để tầng use case tự dịch và tự báo lỗi bằng câu chữ của nghiệp vụ, thay vì để bộ
/// nạp JSON của ASP.NET từ chối trước bằng một thông điệp mà người dùng không đọc được.
/// </para>
/// </summary>
public sealed record ChangeAccountStatusCommand(string AccountId, string Status);
