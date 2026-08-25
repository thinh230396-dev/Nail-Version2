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
