using NailManagement.Domain.Enums.Salon;

namespace NailManagement.Domain.Policies;

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
    /// BR-APT-013 — đặt ngoài ca làm việc của kỹ thuật viên chỉ CẢNH BÁO, vẫn cho lưu.
    /// Trả về true nghĩa là có chuyện đáng cảnh báo, không phải là bị chặn.
    /// </summary>
    public static bool IsOutsideShift(TimeOnly shiftStart, TimeOnly shiftEnd, TimeOnly start, TimeOnly end)
        => start < shiftStart || end > shiftEnd;
}
