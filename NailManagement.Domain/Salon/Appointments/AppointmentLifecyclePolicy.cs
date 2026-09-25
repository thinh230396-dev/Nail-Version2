namespace NailManagement.Domain.Salon.Appointments;

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
    /// BR-APT-026 — lịch hẹn được phép tự hoàn tất khi hóa đơn gắn với nó thu đủ tiền.
    /// <para>
    /// Cố ý là một phép hỏi RIÊNG chứ không phải thêm một ô vào bảng <see cref="Allowed"/>:
    /// bảng đó trả lời câu hỏi "người ở quầy bấm được nút nào", và <see cref="NextStates"/>
    /// đọc nó để gửi danh sách nút cho giao diện. Nhét <c>Completed</c> vào hàng
    /// <c>CheckedIn</c> sẽ khiến màn hình mọc ra một nút "Hoàn tất" mà lễ tân bấm vào sẽ bị
    /// từ chối — luật ở đây không cho ai bấm tay, nó chỉ mở đường cho tiền đã thu.
    /// </para>
    /// <para>
    /// <b>Có cả <c>CheckedIn</c></b> — quyết định 57 chốt ngày 13. Sơ đồ mục 16.1 chỉ vẽ một
    /// mũi tên tới <c>Completed</c> và nó xuất phát từ <c>InService</c>, nhưng BR-INV-010 lại
    /// cho lập hóa đơn từ cả lịch đang <c>CheckedIn</c>. Khách check-in rồi trả tiền luôn —
    /// chuyện thường ở tiệm nail — rơi đúng vào khoảng trống giữa hai luật, và nếu giữ nguyên
    /// sơ đồ thì <b>lần thu tiền sẽ thất bại</b>. Từ chối tiền thật của khách vì một mũi tên
    /// thiếu trong sơ đồ là cái giá không đáng trả.
    /// </para>
    /// </summary>
    public static bool CanCompleteFromPayment(AppointmentStatus status)
        => status is AppointmentStatus.CheckedIn or AppointmentStatus.InService;

    /// <summary>
    /// BR-APT-027 — chủ tiệm đóng tay một lịch đang phục vụ dở khi hóa đơn chưa thu đủ.
    /// <para>
    /// Hẹp hơn <see cref="CanCompleteFromPayment"/> đúng một trạng thái, và đó là chủ đích:
    /// ngoại lệ này nói về buổi làm <b>đã bắt đầu</b> mà tiền chưa đủ. Một lịch còn ở
    /// <c>CheckedIn</c> thì chưa ai đụng vào khách, nên đường đúng của nó là chuyển sang
    /// <c>InService</c> như mọi ngày chứ không phải đóng tắt.
    /// </para>
    /// </summary>
    public static bool CanForceComplete(AppointmentStatus status)
        => status == AppointmentStatus.InService;

    /// <summary>
    /// BR-APT-023 — lịch hẹn đã hoàn tất thì không sửa được trường nào; muốn điều chỉnh
    /// phải xử lý qua hóa đơn.
    /// BR-APT-025 — dời lịch chỉ được phép khi còn ở Pending hoặc Confirmed.
    /// </summary>
    public static bool CanReschedule(AppointmentStatus status)
        => status is AppointmentStatus.Pending or AppointmentStatus.Confirmed;
}
