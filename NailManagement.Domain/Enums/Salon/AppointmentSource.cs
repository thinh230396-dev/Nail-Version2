namespace NailManagement.Domain.Enums.Salon;

/// <summary>
/// BR-APT-007 — nguồn lịch hẹn do lễ tân CHỌN TAY.
/// <para>
/// Không có hệ thống nào tự sinh lịch hẹn <c>Online</c>: vai trò Khách hàng nằm ngoài
/// phạm vi MVP (BR-AUTH-003), nên giá trị này chỉ ghi lại việc khách nhắn qua trang web.
/// </para>
/// </summary>
public enum AppointmentSource
{
    Reception = 1,
    Phone = 2,
    Zalo = 3,
    Online = 4
}
