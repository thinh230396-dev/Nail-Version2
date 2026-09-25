namespace NailManagement.Domain.Salon.Branches;

/// <summary>
/// BR-BRANCH-003/004 — "xóa chi nhánh" thực chất là chuyển sang <c>Inactive</c>.
/// Chi nhánh <c>Inactive</c> không tạo lịch hẹn mới được, nhưng dữ liệu cũ vẫn xem và
/// vẫn vào báo cáo bình thường (BR-DEL-003).
/// </summary>
public enum BranchStatus
{
    Active = 1,
    Inactive = 2
}
