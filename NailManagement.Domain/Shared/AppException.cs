namespace NailManagement.Domain.Shared;

/// <summary>Một ô nhập bị sai, để frontend gắn thông báo vào đúng chỗ.</summary>
/// <param name="Field">Tên trường đúng như frontend đặt, ví dụ <c>identifier</c>, <c>startAt</c>, <c>phone</c>.</param>
public sealed record FieldError(string Field, string Message);

/// <summary>
/// Lỗi nghiệp vụ có chủ đích — phân biệt với lỗi lập trình (bug).
/// <para>
/// Cố ý KHÔNG có thuộc tính HTTP status: tầng Domain và Application không được biết gì về HTTP.
/// Việc ánh xạ <see cref="Code"/> sang status nằm ở <c>NailManagement.API/Common/ApiExceptionHandler.cs</c>.
/// </para>
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string code, string message, IReadOnlyList<FieldError>? fields = null)
        : base(message)
    {
        Code = code;
        Fields = fields ?? [];
    }

    /// <summary>Một trong các hằng số ở <see cref="ErrorCode"/>.</summary>
    public string Code { get; }

    public IReadOnlyList<FieldError> Fields { get; }
}
