namespace NailManagement.Domain.Access;

/// <summary>
/// BR-SUB-007 — nhóm chức năng nào bị khóa theo gói đăng ký, và khóa bằng quyền tên gì.
/// <para>
/// Bảng này CỐ Ý ngắn. Ở frontend có chín trang gắn khóa theo gói, nhưng bảy trong số đó
/// thuộc các module giữ nguyên <c>localStorage</c> ở MVP (kho vật tư, thu chi, loyalty,
/// thư viện mẫu, vệ sinh, đặt lịch online) — chúng không có endpoint nào để mà chặn.
/// </para>
/// <para>
/// Báo cáo doanh thu cũng cố ý không nằm đây: ở frontend, gói thiếu quyền
/// <c>advanced_reports</c> chỉ bị hạ xuống mức xem rút gọn chứ không bị khóa hẳn, nên chặn
/// ở tầng API sẽ làm gói Basic mất luôn báo cáo cơ bản — chặt hơn cả bảng giá đã hứa.
/// </para>
/// <para>
/// Chi nhánh, nhân viên và dịch vụ không khóa theo quyền mà bị chặn theo <b>hạn mức số
/// lượng</b> của gói (BR-BRANCH-005, BR-EMP-008), là một cơ chế khác hẳn.
/// </para>
/// </summary>
public static class FeatureCapabilityPolicy
{
    private static readonly Dictionary<Feature, string> RequiredCapability = new()
    {
        [Feature.Appointments] = "appointments",
        [Feature.Customers] = "customers"
    };

    /// <summary>Trả về tên quyền mà gói phải mở, hoặc null nếu nhóm chức năng này không khóa theo gói.</summary>
    public static string? CapabilityFor(Feature feature)
        => RequiredCapability.TryGetValue(feature, out var capability) ? capability : null;

    /// <param name="enabledCapabilities">Danh sách quyền mà gói của tiệm đang mở.</param>
    public static bool IsUnlocked(Feature feature, IReadOnlyCollection<string> enabledCapabilities)
    {
        var required = CapabilityFor(feature);

        return required is null || enabledCapabilities.Contains(required);
    }
}
