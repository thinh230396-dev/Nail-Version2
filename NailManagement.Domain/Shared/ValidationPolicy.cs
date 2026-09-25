namespace NailManagement.Domain.Shared;

/// <summary>
/// Các con số ở bảng BR-VAL-001, gom về một chỗ.
/// <para>
/// Đặt ở tầng Domain chứ không phải <c>appsettings.json</c>: đây là quyết định nghiệp vụ
/// (một buổi làm nail không thể dài quá tám tiếng), không phải tham số triển khai. Để
/// trong cấu hình là mời người khác đổi mà không ai xem lại rule.
/// </para>
/// </summary>
public static class ValidationPolicy
{
    public const int BranchNameMinLength = 3;
    public const int BranchNameMaxLength = 80;

    /// <summary>Thời lượng dịch vụ tối đa 480 phút — tám tiếng, dài hơn là nhập nhầm.</summary>
    public const int ServiceMaxDurationMinutes = 480;

    public const int ServiceMinDurationMinutes = 1;

    /// <summary>Thời gian dọn dẹp giữa hai lịch hẹn, tối đa 60 phút.</summary>
    public const int ServiceMaxBufferMinutes = 60;

    public const int NameMaxLength = 160;
    public const int LongTextMaxLength = 1000;

    /// <summary>Ngưỡng hạng khách ở BR-CUS-007, đơn vị VND.</summary>
    public const long CustomerLoyalThreshold = 5_000_000;

    public const long CustomerVipThreshold = 20_000_000;
}
