using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Common;
using NailManagement.Domain.Salon.StaffMembers;

namespace NailManagement.Application.Features.Staff;

/// <summary>
/// BR-EMP-008 — cưỡng chế hạn mức <c>max_staff</c> của gói đăng ký.
/// <para>
/// Tách thành một lớp dùng chung vì hạn mức này có <b>hai đường vào</b>: thêm hồ sơ mới, và
/// nhận lại một người đã nghỉ việc. Ngày 5 đã trả giá cho bài học này ở phía chi nhánh —
/// đường "bật lại" quên kiểm hạn mức, và một tiệm gói Premium chạy được 4 chi nhánh trên
/// hạn mức 3 chỉ bằng cách ngừng rồi bật lại. Một phép đếm dùng chung thì hai đường vào
/// không thể cho ra hai kết quả khác nhau.
/// </para>
/// <para>
/// Đếm <i>tại thời điểm thao tác</i> chứ không lưu sẵn một con số: lưu sẵn thì con số đó
/// lệch ngay lần đầu có ai nghỉ việc.
/// </para>
/// </summary>
public sealed class StaffQuotaGuard(
    IStaffRepository staffMembers,
    TenantPlanReader plans)
{
    /// <param name="isReactivating">
    /// Chỉ đổi câu chữ của thông báo, không đổi phép kiểm. Người đang thêm nhân viên mới
    /// cần nghe "nâng gói"; người đang nhận lại nhân viên cũ cần biết họ còn một lựa chọn
    /// nữa là cho người khác nghỉ trước.
    /// </param>
    public async Task EnsureRoomForOneMoreAsync(
        string tenantId, bool isReactivating, CancellationToken cancellationToken = default)
    {
        var plan = await plans.GetAsync(tenantId, cancellationToken);

        var activeCount = await staffMembers.CountActiveAsync(cancellationToken);

        if (activeCount < plan.Package.MaxStaff) return;

        throw new LimitExceededException(
            $"Gói {plan.Package.Name} chỉ cho phép {plan.Package.MaxStaff} nhân viên đang làm việc. "
            + (isReactivating
                ? "Cho một nhân viên khác nghỉ việc hoặc nâng gói trước khi nhận lại người này."
                : "Nâng gói để thêm nhân viên mới."));
    }
}
