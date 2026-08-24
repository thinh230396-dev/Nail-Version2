using NailManagement.Domain.Common;

namespace NailManagement.Domain.ValueObjects;

/// <summary>
/// Mật khẩu dạng chữ, chưa băm. Chỉ tồn tại trong bộ nhớ đúng lúc đặt hoặc đổi mật khẩu —
/// không bao giờ được ghi xuống database hay ghi ra log.
/// <para>
/// Việc băm nằm ở cổng <c>IPasswordHasher</c> của tầng Application, vì thuật toán băm là
/// lựa chọn kỹ thuật, không phải quy tắc nghiệp vụ.
/// </para>
/// </summary>
public sealed class RawPassword
{
    /// <summary>BR-VAL-001 — tối thiểu 8 ký tự, giá trị cố định, không cấu hình được.</summary>
    public const int MinLength = 8;
    public const int MaxLength = 200;

    private RawPassword(string value) => Value = value;

    public string Value { get; }

    public static RawPassword Create(string raw, string field = "password")
    {
        if (string.IsNullOrEmpty(raw))
            throw DomainException.ForField(field, "Mật khẩu không được để trống.");

        if (raw.Length < MinLength)
            throw DomainException.ForField(field, $"Mật khẩu phải có ít nhất {MinLength} ký tự.");

        if (raw.Length > MaxLength)
            throw DomainException.ForField(field, $"Mật khẩu không được dài quá {MaxLength} ký tự.");

        return new RawPassword(raw);
    }

    /// <summary>Chặn mật khẩu lọt vào log một cách vô ý.</summary>
    public override string ToString() => "[RawPassword]";
}
