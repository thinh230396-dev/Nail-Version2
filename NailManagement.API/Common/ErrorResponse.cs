using System.Diagnostics;

namespace NailManagement.API.Common;

/// <summary>Một ô nhập bị sai, dạng gửi ra ngoài.</summary>
public sealed record FieldErrorDto(string Field, string Message);

/// <param name="TraceId">
/// Mã truy vết của request — cùng giá trị với trường <c>TraceId</c> trong log máy chủ. Người dùng
/// chép mã này khi báo lỗi là đủ để tìm đúng dòng log, không phải đoán theo giờ.
/// </param>
public sealed record ErrorBody(
    string Code,
    string Message,
    IReadOnlyList<FieldErrorDto> Fields,
    string? TraceId = null);

/// <summary>
/// Thân lỗi trả về, đúng contract đã chốt:
/// <code>{ "error": { "code": "...", "message": "...", "fields": [...], "traceId": "..." } }</code>
/// <c>traceId</c> là trường thêm vào sau; client cũ không đọc nó vẫn chạy đúng.
/// </summary>
public sealed record ErrorResponse(ErrorBody Error)
{
    public static ErrorResponse Of(
        HttpContext context, string code, string message, IReadOnlyList<FieldErrorDto>? fields = null)
        => new(new ErrorBody(code, message, fields ?? [], TraceIdOf(context)));

    // Activity.Current là thứ bộ ghi log dùng cho phạm vi request; lấy cùng nguồn thì hai giá trị
    // luôn khớp. TraceIdentifier chỉ là phương án lùi khi không có Activity nào.
    private static string TraceIdOf(HttpContext context)
        => Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
}
