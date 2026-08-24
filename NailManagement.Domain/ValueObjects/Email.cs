using System.Text.RegularExpressions;
using NailManagement.Domain.Common;

namespace NailManagement.Domain.ValueObjects;

/// <summary>
/// Email đã được kiểm tra định dạng. Đã tạo được đối tượng này thì không cần kiểm tra
/// lại ở bất kỳ đâu phía sau. BR-VAL-001 — email duy nhất toàn hệ thống, đúng định dạng.
/// </summary>
public sealed partial record Email
{
    public const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(string raw, string field = "email")
    {
        var normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized.Length == 0)
            throw DomainException.ForField(field, "Email không được để trống.");

        if (normalized.Length > MaxLength)
            throw DomainException.ForField(field, $"Email không được dài quá {MaxLength} ký tự.");

        if (!Pattern().IsMatch(normalized))
            throw DomainException.ForField(field, "Email không đúng định dạng.");

        return new Email(normalized);
    }

    /// <summary>Dùng khi đọc từ database — dữ liệu đã hợp lệ lúc ghi vào.</summary>
    public static Email FromPersistence(string value) => new(value);

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$")]
    private static partial Regex Pattern();
}
