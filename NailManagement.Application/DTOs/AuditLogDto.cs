namespace NailManagement.Application.DTOs;

/// <summary>
/// Một dòng nhật ký kiểm toán gửi ra ngoài — BR-AUD-003.
/// <para>
/// Chỉ có DTO đọc, không có DTO ghi: BR-AUD-004 quy định nhật ký không sửa và không xóa,
/// nên không tồn tại đường nào để client gửi một bản ghi vào đây. Sự kiện chỉ sinh ra từ
/// bên trong máy chủ, ở chính use case thực hiện thao tác tương ứng.
/// </para>
/// </summary>
public sealed record AuditLogDto(
    string Id,
    string Event,
    string? ActorUserId,
    string? ActorRole,
    string? TenantId,
    string? TargetType,
    string? TargetId,
    string? Ip,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string> Metadata);
