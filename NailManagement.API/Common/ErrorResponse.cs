namespace NailManagement.API.Common;

/// <summary>Một ô nhập bị sai, dạng gửi ra ngoài.</summary>
public sealed record FieldErrorDto(string Field, string Message);

public sealed record ErrorBody(string Code, string Message, IReadOnlyList<FieldErrorDto> Fields);

/// <summary>
/// Thân lỗi trả về, đúng contract đã chốt:
/// <code>{ "error": { "code": "...", "message": "...", "fields": [...] } }</code>
/// </summary>
public sealed record ErrorResponse(ErrorBody Error);
