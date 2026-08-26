using NailManagement.Domain.Enums.Salon;

namespace NailManagement.Domain.Policies;

/// <summary>
/// Sơ đồ chuyển trạng thái lịch hẹn ở mục 16.1 của tài liệu nghiệp vụ, viết thành dữ liệu
/// thay vì rải rác thành các nhánh điều kiện trong từng use case.
/// <para>
/// BR-APT-022 — mọi chuyển đổi không có trong bảng này đều bị từ chối.
/// </para>
/// </summary>
public static class AppointmentLifecyclePolicy
{
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> Allowed = new()
    {
        [AppointmentStatus.Pending] = [AppointmentStatus.Confirmed, AppointmentStatus.Cancelled],
        [AppointmentStatus.Confirmed] = [AppointmentStatus.CheckedIn, AppointmentStatus.Cancelled, AppointmentStatus.NoShow],
        [AppointmentStatus.CheckedIn] = [AppointmentStatus.InService, AppointmentStatus.Cancelled, AppointmentStatus.NoShow],

        // BR-APT-040 — đang phục vụ dở thì không hủy được, phải kết thúc.
        [AppointmentStatus.InService] = [AppointmentStatus.Completed],

        // BR-APT-041 — ba trạng thái cuối, không quay lại được.
        [AppointmentStatus.Completed] = [],
        [AppointmentStatus.Cancelled] = [],
        [AppointmentStatus.NoShow] = []
    };

    public static bool CanTransition(AppointmentStatus from, AppointmentStatus to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<AppointmentStatus> NextStates(AppointmentStatus from)
        => Allowed.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>BR-APT-041 — không còn đi đâu được nữa.</summary>
    public static bool IsFinal(AppointmentStatus status) => NextStates(status).Count == 0;

    /// <summary>
    /// BR-APT-023 — lịch hẹn đã hoàn tất thì không sửa được trường nào; muốn điều chỉnh
    /// phải xử lý qua hóa đơn.
    /// BR-APT-025 — dời lịch chỉ được phép khi còn ở Pending hoặc Confirmed.
    /// </summary>
    public static bool CanReschedule(AppointmentStatus status)
        => status is AppointmentStatus.Pending or AppointmentStatus.Confirmed;
}
