namespace NailManagement.Domain.Enums.Auth;

/// <summary>
/// Ba mức truy cập trong ma trận mục 3.4, đúng ba ký hiệu mà tài liệu nghiệp vụ dùng:
/// toàn quyền, chỉ xem, không truy cập.
/// </summary>
public enum AccessLevel
{
    /// <summary>Không thấy, không gọi được. Trả về 403.</summary>
    None = 0,

    /// <summary>Đọc được nhưng không ghi được.</summary>
    Read = 1,

    /// <summary>Đọc và ghi.</summary>
    Full = 2
}
