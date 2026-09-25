using System.Linq.Expressions;

namespace NailManagement.Domain.Salon.Appointments;

/// <summary>
/// BR-APT-010/011/012 — luật chống trùng lịch kỹ thuật viên. Đây là một trong bốn thứ mà
/// lộ trình đánh dấu "tuyệt đối không cắt", nên nó nằm ở tầng trong cùng, dưới dạng hàm
/// thuần kiểm thử được mà không cần database.
/// </summary>
public static class AppointmentSchedulePolicy
{
    /// <summary>
    /// BR-APT-010 — khoảng thời gian một lịch hẹn chiếm chỗ bằng tổng thời lượng dịch vụ
    /// CỘNG tổng thời gian dọn dẹp. Quên phần dọn dẹp là xếp hai khách sát nhau đến mức
    /// kỹ thuật viên không kịp trở tay.
    /// </summary>
    public static int TotalMinutes(IEnumerable<(int DurationMinutes, int BufferMinutes)> services)
        => services.Sum(service => service.DurationMinutes + service.BufferMinutes);

    /// <summary>
    /// BR-APT-011 — hai khoảng chồng lấn khi bắt đầu mới sớm hơn kết thúc cũ VÀ kết thúc
    /// mới muộn hơn bắt đầu cũ. Dùng so sánh nghiêm ngặt để hai lịch nối đuôi nhau — cái
    /// này kết thúc đúng lúc cái kia bắt đầu — không bị coi là trùng.
    /// </summary>
    public static bool Overlaps(
        DateTimeOffset startA, DateTimeOffset endA,
        DateTimeOffset startB, DateTimeOffset endB)
        => startA < endB && endA > startB;

    /// <summary>
    /// BR-APT-012 — lịch đã hủy hoặc khách không đến thì không chiếm chỗ của ai, phải loại
    /// khỏi phép kiểm tra ở trên.
    /// </summary>
    public static bool OccupiesSlot(AppointmentStatus status)
        => status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow);

    /// <summary>
    /// Điều kiện "lịch hẹn này đang chặn khoảng giờ kia của kỹ thuật viên kia" — BR-APT-011
    /// và BR-APT-012 gộp lại thành một vế duy nhất, viết dưới dạng cây biểu thức để tầng
    /// lưu trữ dịch được sang SQL.
    /// <para>
    /// Đây là <b>cùng một luật</b> với <see cref="Overlaps"/> và <see cref="OccupiesSlot"/>,
    /// chỉ khác hình dạng. Lý do phải có hình dạng thứ hai: EF Core không dịch được lời gọi
    /// hàm C# nằm trong biểu thức truy vấn, nên nếu không có hàm này thì kho dữ liệu buộc
    /// phải chép tay điều kiện chồng lấn vào câu LINQ của nó — và chống trùng lịch sẽ có hai
    /// định nghĩa ở hai tầng, đúng thứ mà tài liệu nghiệp vụ liệt vào nhóm không được phép sai.
    /// </para>
    /// <para>
    /// Phép so sánh nghiêm ngặt được giữ nguyên: một lịch kết thúc lúc 15:00 không chặn lịch
    /// bắt đầu lúc 15:00. Thời gian dọn dẹp đã nằm sẵn trong <c>EndAt</c> (BR-APT-010) nên
    /// hai lịch sát nhau vẫn có khoảng trở tay.
    /// </para>
    /// </summary>
    /// <param name="exceptAppointmentId">
    /// Chính lịch hẹn đang được sửa hoặc đang được dời. Thiếu tham số này thì một lịch hẹn
    /// lưu lại y nguyên sẽ tự báo mình trùng giờ với chính mình.
    /// </param>
    public static Expression<Func<Appointment, bool>> BlockingSlot(
        string staffId,
        DateTimeOffset start,
        DateTimeOffset end,
        string? exceptAppointmentId)
        => appointment
            => appointment.StaffId == staffId
               && appointment.Status != AppointmentStatus.Cancelled
               && appointment.Status != AppointmentStatus.NoShow
               && appointment.StartAt < end
               && appointment.EndAt > start
               && (exceptAppointmentId == null || appointment.Id != exceptAppointmentId);

    /// <summary>
    /// BR-APT-013 — đặt ngoài ca làm việc của kỹ thuật viên chỉ CẢNH BÁO, vẫn cho lưu.
    /// Trả về true nghĩa là có chuyện đáng cảnh báo, không phải là bị chặn.
    /// </summary>
    public static bool IsOutsideShift(TimeOnly shiftStart, TimeOnly shiftEnd, TimeOnly start, TimeOnly end)
        => start < shiftStart || end > shiftEnd;
}
