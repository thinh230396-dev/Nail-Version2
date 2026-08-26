using System.Globalization;
using System.Text.Json;
using NailManagement.Application.DTOs;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Salon;

namespace NailManagement.Application.Mappings;

/// <summary>Chuyển entity <see cref="Staff"/> sang DTO gửi ra ngoài, và đọc ngược các chuỗi từ client.</summary>
public static class StaffMapper
{
    /// <summary>
    /// Giờ ca làm việc đi ra dưới dạng <c>HH:mm</c>.
    /// <para>
    /// Định dạng cố định theo văn hóa bất biến chứ không theo máy chủ: <c>TimeOnly</c> mặc
    /// định dùng dấu phân cách của hệ điều hành, nên cùng một ca có thể ra "09:00" trên máy
    /// này và "09.00" trên máy khác — đủ để giao diện đọc hỏng.
    /// </para>
    /// </summary>
    private const string ShiftFormat = @"HH:mm";

    private static readonly string[] AcceptedShiftFormats = [@"HH:mm", @"HH:mm:ss"];

    public static string ToWireFormat(StaffRole role) => role switch
    {
        StaffRole.Technician => "TECHNICIAN",
        StaffRole.Receptionist => "RECEPTIONIST",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Vai trò nhân viên không hợp lệ.")
    };

    /// <summary>BR-EMP-005 — bốn trạng thái, không hơn.</summary>
    public static string ToWireFormat(StaffStatus status) => status switch
    {
        StaffStatus.Working => "WORKING",
        StaffStatus.OffShift => "OFF_SHIFT",
        StaffStatus.Leave => "LEAVE",
        StaffStatus.Inactive => "INACTIVE",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái nhân viên không hợp lệ.")
    };

    public static StaffRole ParseRole(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "TECHNICIAN" => StaffRole.Technician,
            "RECEPTIONIST" => StaffRole.Receptionist,
            _ => throw DomainException.ForField(
                "role", "Vai trò nhân viên chỉ nhận TECHNICIAN hoặc RECEPTIONIST.")
        };

    public static StaffStatus ParseStatus(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "WORKING" => StaffStatus.Working,
            "OFF_SHIFT" => StaffStatus.OffShift,
            "LEAVE" => StaffStatus.Leave,
            "INACTIVE" => StaffStatus.Inactive,
            _ => throw DomainException.ForField(
                "status", "Trạng thái nhân viên chỉ nhận WORKING, OFF_SHIFT, LEAVE hoặc INACTIVE.")
        };

    /// <param name="field">Tên ô nhập để gắn thông báo lỗi, <c>shiftStart</c> hoặc <c>shiftEnd</c>.</param>
    public static TimeOnly ParseShift(string? value, string field, string label)
    {
        var trimmed = (value ?? string.Empty).Trim();

        if (!TimeOnly.TryParseExact(
                trimmed, AcceptedShiftFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw DomainException.ForField(field, $"{label} phải ở dạng HH:mm, ví dụ 09:00.");
        }

        return parsed;
    }

    /// <summary>
    /// BR-EMP-010 — danh sách kỹ năng chỉ để hiển thị, nên nó được lưu thành một cột JSON
    /// thay vì một bảng nối. Bảng nối chỉ đáng có khi cần truy vấn ngược "ai làm được dịch
    /// vụ này", mà đó lại đúng là thứ rule nói hệ thống không cưỡng chế.
    /// </summary>
    public static string ToSkillsJson(IReadOnlyList<string>? skills)
    {
        var cleaned = (skills ?? [])
            .Select(skill => (skill ?? string.Empty).Trim())
            .Where(skill => skill.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return JsonSerializer.Serialize(cleaned);
    }

    /// <summary>
    /// Đọc lại cột JSON. Hỏng thì trả danh sách rỗng chứ không ném lỗi: kỹ năng là thông tin
    /// hiển thị, và làm cả màn hình nhân viên sập vì một ô phụ bị lệch dữ liệu là đánh đổi sai.
    /// </summary>
    public static IReadOnlyList<string> ParseSkills(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <param name="account">
    /// Tài khoản đăng nhập gắn với hồ sơ này, hoặc null khi chưa cấp. Truyền vào từ ngoài
    /// thay vì để mapper tự đi tra: một mapper chạm vào kho dữ liệu là một mapper không
    /// dùng lại được ở chỗ đã có sẵn dữ liệu.
    /// </param>
    public static StaffDto ToDto(Staff staff, AppUser? account) => new(
        staff.Id,
        staff.TenantId,
        staff.BranchId,
        staff.FullName,
        staff.Phone?.Value,
        staff.Email,
        ToWireFormat(staff.Role),
        ToWireFormat(staff.Status),
        staff.ShiftStart.ToString(ShiftFormat, CultureInfo.InvariantCulture),
        staff.ShiftEnd.ToString(ShiftFormat, CultureInfo.InvariantCulture),
        staff.CommissionRate,
        ParseSkills(staff.SkillsJson),
        account is null
            ? null
            : new StaffAccountDto(
                account.Id,
                account.Email.Value,
                account.Username,
                TenantMapper.ToWireFormat(account.Status)),
        staff.CreatedAt,
        staff.UpdatedAt);
}
