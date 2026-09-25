namespace NailManagement.Domain.Shared;

/// <summary>
/// Vi phạm một quy tắc nghiệp vụ cốt lõi — thứ luôn đúng bất kể ứng dụng nào dùng đến
/// entity này. Ví dụ: email sai định dạng, mật khẩu ngắn hơn 8 ký tự (BR-VAL-001).
/// <para>
/// Khác với lỗi ở tầng Application: lỗi ở đây thuộc về bản thân dữ liệu, không phụ thuộc
/// vào tình huống sử dụng.
/// </para>
/// </summary>
public sealed class DomainException : AppException
{
    public DomainException(string message, IReadOnlyList<FieldError>? fields = null)
        : base(ErrorCode.ValidationFailed, message, fields)
    {
    }

    /// <summary>Lỗi gắn với đúng một ô nhập.</summary>
    public static DomainException ForField(string field, string message)
        => new(message, [new FieldError(field, message)]);
}
