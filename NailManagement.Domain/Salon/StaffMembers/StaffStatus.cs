namespace NailManagement.Domain.Salon.StaffMembers;

/// <summary>
/// BR-EMP-005 — bốn trạng thái nhân viên. BR-EMP-006: "nghỉ việc" là <c>Inactive</c>,
/// không xóa bản ghi, để tên nhân viên vẫn hiện đúng trong lịch hẹn và hóa đơn cũ.
/// </summary>
public enum StaffStatus
{
    Working = 1,
    OffShift = 2,
    Leave = 3,
    Inactive = 4
}
