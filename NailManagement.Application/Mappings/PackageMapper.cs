using System.Text.Json;
using NailManagement.Application.DTOs;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Enums.Platform;

namespace NailManagement.Application.Mappings;

/// <summary>
/// Chuyển entity <see cref="Package"/> sang DTO, và là nơi DUY NHẤT biết cách đọc cột JSON
/// quyền tính năng.
/// <para>
/// Phép đọc đó nằm ở đây thay vì ở <see cref="TenantMapper"/> vì cả hai chỗ đều cần tới nó:
/// bảng giá hiển thị gói mở những gì, còn phiên đăng nhập cần đúng danh sách ấy để trả lời
/// bước 2 của BR-TENANT-013. Viết hai lần thì sớm muộn cũng có một bản đọc sai tên khóa, và
/// hậu quả là quyền tính năng lệch nhau giữa hai màn hình mà không ai biết vì sao.
/// </para>
/// </summary>
public static class PackageMapper
{
    // Cột JSON viết theo kiểu camelCase của frontend, còn thuộc tính C# viết hoa chữ đầu.
    // Không bật tùy chọn này thì mọi quyền đều đọc ra false, và hậu quả là mọi tiệm đều
    // mất sạch tính năng — một lỗi im lặng, không có ngoại lệ nào được ném ra.
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Bốn trạng thái gói ở BR-SUB-001. Cả bốn đều gửi ra ngoài chứ không gộp lại, vì chúng
    /// nói ba chuyện khác nhau với người bán gói: <c>Deprecated</c> vẫn phục vụ tiệm đang
    /// dùng nhưng không nhận đăng ký mới (BR-SUB-002), còn <c>Archived</c> là đã gỡ hẳn khỏi
    /// bảng giá (BR-SUB-003).
    /// </summary>
    public static string ToWireFormat(PackageStatus status) => status switch
    {
        PackageStatus.Draft => "DRAFT",
        PackageStatus.Active => "ACTIVE",
        PackageStatus.Deprecated => "DEPRECATED",
        PackageStatus.Archived => "ARCHIVED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái gói không hợp lệ.")
    };

    public static string ToWireFormat(BillingCycle cycle) => cycle switch
    {
        BillingCycle.Monthly => "MONTHLY",
        BillingCycle.Yearly => "YEARLY",
        _ => throw new ArgumentOutOfRangeException(nameof(cycle), cycle, "Chu kỳ thanh toán không hợp lệ.")
    };

    /// <summary>Đọc ngược từ chuỗi của frontend. Chuỗi lạ thì mặc định theo tháng.</summary>
    public static BillingCycle ParseBillingCycle(string? raw)
        => (raw ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "YEARLY" or "YEAR" or "ANNUAL" => BillingCycle.Yearly,
            _ => BillingCycle.Monthly
        };

    public static PackageDto ToDto(Package package) => new(
        package.Id,
        package.Name,
        package.Description,
        package.Price,
        ToWireFormat(package.BillingCycle),
        package.MaxSalons,
        package.MaxStaff,
        package.Version,
        ToWireFormat(package.Status),
        package.Color,
        ReadCapabilities(package),
        package.FeaturesJson,
        package.LimitsJson);

    /// <summary>
    /// Đọc danh sách quyền đang bật từ cột JSON của gói.
    /// <para>
    /// Cột này chép nguyên hình dạng bảng quyền của frontend: một mảng các mục
    /// <c>{ key, label, enabled }</c>. Ở đây chỉ lấy những mục đang bật, vì phần nhãn chỉ
    /// phục vụ hiển thị trên bảng giá.
    /// </para>
    /// <para>
    /// JSON hỏng thì trả về danh sách rỗng chứ không ném lỗi: hậu quả là tiệm tạm thời bị
    /// khóa tính năng — phiền nhưng an toàn. Ném lỗi ở đây sẽ làm sập mọi request của tiệm
    /// đó, kể cả trang gia hạn gói, tức là khóa luôn đường tự sửa.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> ReadCapabilities(Package package)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<CapabilityRow[]>(package.CapabilitiesJson, JsonOptions);

            return parsed is null
                ? []
                : [.. parsed.Where(row => row.Enabled).Select(row => row.Key)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record CapabilityRow(string Key, bool Enabled);
}
