using NailManagement.Application.Common;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Domain.Salon.Branches;

namespace NailManagement.Application.Features.Branches;

/// <summary>
/// BR-BRANCH-005 — cưỡng chế hạn mức <c>max_salons</c> của gói đăng ký.
/// <para>
/// Cùng khuôn với <c>StaffQuotaGuard</c>, và vì cùng một lý do: hạn mức này có <b>hai đường
/// vào</b> — tạo chi nhánh mới và bật lại một chi nhánh đã ngừng. Ngày 5 đã trả giá khi đường
/// "bật lại" quên kiểm hạn mức; một phép đếm dùng chung thì hai đường vào không thể cho ra hai
/// kết quả khác nhau.
/// </para>
/// <para>
/// Đếm chi nhánh <i>đang hoạt động</i> tại thời điểm thao tác, không lưu sẵn một con số:
/// BR-BRANCH-004 cho chi nhánh đã ngừng ở lại trong dữ liệu để lịch hẹn và hóa đơn cũ vẫn đọc
/// đúng tên, và giữ chỗ hạn mức cho chúng là phạt tiệm vì đã đóng cửa một điểm.
/// </para>
/// </summary>
public sealed class BranchQuotaGuard(IBranchRepository branches, TenantPlanReader plans)
{
    /// <param name="isReactivating">
    /// Chỉ đổi câu chữ của thông báo: người bật lại một chi nhánh cũ còn một lựa chọn nữa là
    /// ngừng chi nhánh khác trước.
    /// </param>
    public async Task EnsureRoomForOneMoreAsync(
        string tenantId, bool isReactivating, CancellationToken cancellationToken = default)
    {
        var plan = await plans.GetAsync(tenantId, cancellationToken);

        var activeCount = await branches.CountActiveAsync(cancellationToken);

        if (activeCount < plan.Package.MaxSalons) return;

        throw new LimitExceededException(
            $"Gói {plan.Package.Name} chỉ cho phép {plan.Package.MaxSalons} chi nhánh đang hoạt động. "
            + (isReactivating
                ? "Ngừng một chi nhánh khác hoặc nâng gói trước khi bật lại chi nhánh này."
                : "Nâng gói để thêm chi nhánh mới."));
    }
}
