namespace NailManagement.Application.DTOs.Auditing;

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
    /// <summary>
    /// Tên người thao tác, tra từ bảng tài khoản <b>lúc đọc</b> — không phải một cột được lưu.
    /// <para>
    /// Bảng nhật ký cố ý chỉ giữ <c>ActorUserId</c>: tên đổi được còn mã thì không, nên chép
    /// tên vào bản ghi là để nó nói sai về quá khứ ngay lần đầu ai đó đổi tên hiển thị. Đổi lại,
    /// tên ở đây luôn là tên <i>hiện tại</i> của người ấy, và đó là thứ người đọc sổ cần để biết
    /// hôm nay phải đi hỏi ai.
    /// </para>
    /// <para>
    /// Rỗng khi bản ghi không có người thực hiện, hoặc khi tài khoản không còn tra ra được. Màn
    /// hình khi ấy hiển thị lại <c>ActorUserId</c> — một dòng nhật ký không nói được ai làm thì
    /// vô dụng, nên thà hiện mã còn hơn để trống.
    /// </para>
    /// </summary>
    string? ActorDisplayName,
    string? ActorRole,
    string? TenantId,
    string? TargetType,
    string? TargetId,
    string? Ip,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string> Metadata);
