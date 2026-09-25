namespace NailManagement.Domain.Salon.Appointments;

/// <summary>
/// BR-APT-020 — bảy trạng thái lịch hẹn. Bỏ <c>REFUNDED</c>: hoàn tiền là chuyện của hóa đơn.
/// Sơ đồ chuyển trạng thái nằm ở <c>AppointmentLifecyclePolicy</c>.
/// </summary>
public enum AppointmentStatus
{
    Pending = 1,
    Confirmed = 2,
    CheckedIn = 3,
    InService = 4,
    Completed = 5,
    Cancelled = 6,
    NoShow = 7
}
