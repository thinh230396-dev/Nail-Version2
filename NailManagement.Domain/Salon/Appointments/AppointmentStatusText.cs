using NailManagement.Domain.Shared;

namespace NailManagement.Domain.Salon.Appointments;

/// <summary>
/// Tên tiếng Việt của bảy trạng thái lịch hẹn, dùng trong <b>thông báo lỗi gửi tới người
/// dùng</b>.
/// <para>
/// Đặt ở tầng Domain vì đây là nơi luật chuyển trạng thái được cưỡng chế, và
/// <c>DomainException</c> mang theo chính câu chữ mà người ở quầy sẽ đọc — giống hệt cách
/// <see cref="Guard"/> đã viết thông báo tiếng Việt ngay trong tầng này. Ném ra tên
/// hằng số của C# thì lễ tân nhận được "không thể chuyển từ Pending sang CheckedIn", một câu
/// vừa sai ngôn ngữ vừa dùng những chữ không xuất hiện ở bất kỳ đâu trên màn hình.
/// </para>
/// <para>
/// Khác <c>AppointmentMapper.ToWireFormat</c> ở tầng Application, thứ sinh ra chuỗi
/// <c>PENDING</c> / <c>CHECKED_IN</c> cho <b>máy</b> đọc. Hai bảng phục vụ hai người đọc khác
/// nhau nên cố ý không gộp: gộp lại là buộc một trong hai bên phải nhận thứ mình không dùng được.
/// </para>
/// </summary>
public static class AppointmentStatusText
{
    /// <summary>
    /// Câu chữ khớp với nhãn mà giao diện đang hiện ở <c>StatusBadge</c>, để một lỗi từ máy
    /// chủ và một cái huy hiệu trên cùng màn hình không gọi một trạng thái bằng hai cái tên.
    /// </summary>
    public static string Label(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "Chờ xác nhận",
        AppointmentStatus.Confirmed => "Đã xác nhận",
        AppointmentStatus.CheckedIn => "Đã đến",
        AppointmentStatus.InService => "Đang phục vụ",
        AppointmentStatus.Completed => "Hoàn thành",
        AppointmentStatus.Cancelled => "Đã hủy",
        AppointmentStatus.NoShow => "Khách không đến",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái lịch hẹn không hợp lệ.")
    };
}
