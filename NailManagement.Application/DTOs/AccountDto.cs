using System.Text.Json.Serialization;

namespace NailManagement.Application.DTOs;

/// <summary>
/// Hình dạng tài khoản mà frontend nhận được.
/// <para>
/// Khớp đúng <c>DemoAccount</c> ở <c>src/auth/demoAccounts.ts</c> của frontend, kể cả cách
/// viết hoa thường, để việc chuyển backend không làm vỡ giao diện đang chạy. Vai trò gửi đi
/// dưới dạng chuỗi <c>SUPERADMIN</c> / <c>TENANT_ADMIN</c> / <c>RECEPTIONIST</c>.
/// </para>
/// <para>
/// DTO cố ý <b>không</b> phải entity <c>AppUser</c>: entity có <c>PasswordHash</c>,
/// <c>PasswordSalt</c>, <c>FailedAttempts</c> — những thứ không bao giờ được ra khỏi máy chủ.
/// Tách DTO là hàng rào ngăn chuyện đó xảy ra do vô ý.
/// </para>
/// </summary>
public sealed record AccountDto
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required string DisplayName { get; init; }

    // Bốn trường dưới chỉ xuất hiện khi có giá trị, đúng như frontend đang mong đợi
    // (chúng là optional trong DemoAccount). ⚠️ Tạm thời — xem ghi chú ở AppUser.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TenantId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TenantName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BranchCode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BranchName { get; init; }
}
