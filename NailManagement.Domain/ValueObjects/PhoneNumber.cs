using System.Text.RegularExpressions;
using NailManagement.Domain.Common;

namespace NailManagement.Domain.ValueObjects;

/// <summary>
/// Số điện thoại Việt Nam đã được kiểm tra định dạng — BR-VAL-001, mẫu <c>^(\+84|0)\d{9,10}$</c>.
/// <para>
/// Có value object riêng vì đây là <b>khóa tra cứu khách hàng</b> (BR-CUS-002: duy nhất
/// trong phạm vi một tenant, và BR-CUS-003: là trường bắt buộc duy nhất khi tạo khách).
/// Một chuỗi lệch một dấu cách là một khách hàng bị trùng.
/// </para>
/// </summary>
public sealed partial record PhoneNumber
{
    public const int MaxLength = 20;

    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    public static PhoneNumber Create(string raw, string field = "phone")
    {
        // Bỏ mọi khoảng trắng, dấu chấm và gạch nối: lễ tân gõ "090 123 4567" hay
        // "090-123-4567" đều phải ra cùng một khách hàng.
        var normalized = Separators().Replace(raw ?? string.Empty, string.Empty);

        if (normalized.Length == 0)
            throw DomainException.ForField(field, "Số điện thoại không được để trống.");

        if (!Pattern().IsMatch(normalized))
            throw DomainException.ForField(
                field,
                "Số điện thoại không đúng định dạng. Ví dụ hợp lệ: 0901234567 hoặc +84901234567.");

        return new PhoneNumber(normalized);
    }

    /// <summary>Dùng khi đọc từ database — dữ liệu đã hợp lệ lúc ghi vào.</summary>
    public static PhoneNumber FromPersistence(string value) => new(value);

    public override string ToString() => Value;

    [GeneratedRegex(@"[\s.\-()]")]
    private static partial Regex Separators();

    [GeneratedRegex(@"^(\+84|0)\d{9,10}$")]
    private static partial Regex Pattern();
}
