using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Branches;
using NailManagement.Domain.Salon.Branches;
using NailManagement.Domain.Shared;

namespace NailManagement.Application.Features.Branches.UseCases;

/// <summary>
/// Ngừng hoặc mở lại một chi nhánh — BR-BRANCH-004.
/// <para>
/// Đây chính là thứ mà giao diện gọi là "xóa chi nhánh". Không có lệnh xóa cứng nào
/// (BR-DEL-001): chi nhánh đã ngừng vẫn ở lại để lịch hẹn và hóa đơn cũ đọc đúng tên
/// (BR-DEL-003), nó chỉ không nhận lịch hẹn mới nữa.
/// </para>
/// <para>
/// BR-BRANCH-002 — chi nhánh chính không ngừng được. Phép chặn đó nằm trong
/// <c>Branch.Deactivate</c>, tức là ở entity chứ không ở đây, vì nó luôn đúng bất kể ai gọi
/// tới và gọi từ màn hình nào.
/// </para>
/// <para>
/// <b>Bật lại cũng phải qua hạn mức</b> — BR-BRANCH-005. Bản đầu tiên của use case này cố ý
/// bỏ qua phép kiểm đó, với lý do sợ tiệm vừa hạ gói bị kẹt. Rà soát ngày 25/08 cho thấy lý
/// do ấy không đứng vững, và lỗ hổng thì có thật: một tiệm gói Premium đã chạy được
/// <b>4 chi nhánh trên hạn mức 3</b> chỉ bằng cách ngừng rồi bật lại. Kiểm ở đây không làm
/// kẹt ai — chi nhánh <i>đang</i> hoạt động không bị đụng tới, tiệm chỉ không bật thêm được
/// cái mới, đúng như khi họ muốn tạo mới.
/// </para>
/// </summary>
public sealed class ChangeBranchStatusUseCase(
    IBranchRepository branches,
    BranchQuotaGuard quota,
    ITenantContext tenantContext,
    IClock clock)
{
    public async Task<BranchDto> ExecuteAsync(
        ChangeBranchStatusCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var status = (command.Status ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => BranchStatus.Active,
            "INACTIVE" => BranchStatus.Inactive,
            _ => throw DomainException.ForField(
                "status", "Trạng thái chi nhánh chỉ nhận ACTIVE hoặc INACTIVE.")
        };

        var branch = await branches.FindByIdAsync(command.BranchId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chi nhánh.");

        if (status == BranchStatus.Inactive)
        {
            branch.Deactivate(now);
        }
        else
        {
            // Chỉ kiểm khi đây thật sự là một lần BẬT LẠI. Gửi ACTIVE cho chi nhánh vốn đã
            // hoạt động là thao tác không đổi gì, mà phép đếm khi đó lại tính cả chính nó —
            // tiệm đang dùng vừa đủ hạn mức sẽ bị từ chối một việc họ không hề làm.
            if (branch.Status != BranchStatus.Active)
                await quota.EnsureRoomForOneMoreAsync(
                    tenantContext.ActiveTenantId ?? throw new TenantNotSelectedException(),
                    isReactivating: true,
                    cancellationToken);

            branch.Activate(now);
        }

        await branches.UpdateAsync(branch, cancellationToken);

        return BranchMapper.ToDto(branch);
    }
}
