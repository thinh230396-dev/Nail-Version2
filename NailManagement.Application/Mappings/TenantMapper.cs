using System.Text.Json;
using NailManagement.Application.DTOs;
using NailManagement.Domain.Entities;
using NailManagement.Domain.Enums;

namespace NailManagement.Application.Mappings;

/// <summary>
/// Chuyển entity <see cref="Tenant"/> sang DTO gửi ra ngoài.
/// <para>
/// Mọi hàm ở đây nhận thêm tham số thời điểm, vì trạng thái hiển thị và cờ chỉ-đọc đều là
/// kết quả tính lúc đọc (BR-TENANT-002, BR-TENANT-010). Không có "trạng thái tiệm" nào
/// đứng yên để mà chép thẳng ra.
/// </para>
/// </summary>
public static class TenantMapper
{
    // Cột JSON viết theo kiểu camelCase của frontend, còn thuộc tính C# viết hoa chữ đầu.
    // Không bật tùy chọn này thì mọi quyền đều đọc ra false, và hậu quả là mọi tiệm đều
    // mất sạch tính năng — một lỗi im lặng, không có ngoại lệ nào được ném ra.
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Bốn chuỗi trạng thái gửi cho frontend. Frontend đang dùng đúng cách viết này
    /// (<c>Tenant['status']</c> trong <c>src/types.ts</c>), nên không được đổi.
    /// </summary>
    public static string ToWireFormat(TenantDisplayStatus status) => status switch
    {
        TenantDisplayStatus.Trial => "TRIAL",
        TenantDisplayStatus.Active => "ACTIVE",
        TenantDisplayStatus.Overdue => "OVERDUE",
        TenantDisplayStatus.Suspended => "SUSPENDED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái tiệm không hợp lệ.")
    };

    public static TenantSummaryDto ToSummary(Tenant tenant, DateTimeOffset now) => new(
        tenant.Id,
        tenant.Code,
        tenant.Name,
        ToWireFormat(tenant.DisplayStatusAt(now)),
        tenant.IsReadOnlyAt(now),
        tenant.ExpiresAt);

    /// <param name="package">
    /// Gói của tiệm. Bắt buộc có: thiếu nó thì không trả lời được bước 2 của BR-TENANT-013,
    /// và bỏ qua bước đó nghĩa là mọi tiệm đều dùng được mọi tính năng.
    /// </param>
    public static TenantScopeDto ToScope(Tenant tenant, Package package, DateTimeOffset now) => new(
        tenant.Id,
        tenant.Code,
        tenant.Name,
        ToWireFormat(tenant.DisplayStatusAt(now)),
        tenant.IsReadOnlyAt(now),
        package.Id,
        package.Name,
        ReadCapabilities(package));

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
    private static IReadOnlyList<string> ReadCapabilities(Package package)
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
