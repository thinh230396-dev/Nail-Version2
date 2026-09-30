using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Branches;
using NailManagement.Domain.Salon.Branches;
using NailManagement.Domain.Shared;

namespace NailManagement.Application.Features.Branches.UseCases;

/// <summary>
/// Thêm chi nhánh cho tiệm đang làm việc — BR-BRANCH-005.
/// <para>
/// Hạn mức <c>max_salons</c> là một trong hai hạn mức được cưỡng chế thật của toàn hệ thống
/// (BR-SUB-005), và nó được đếm <b>tại thời điểm tạo</b> chứ không lưu sẵn một con số. Lưu
/// sẵn thì con số đó sẽ lệch ngay lần đầu có ai ngừng hoạt động một chi nhánh.
/// </para>
/// <para>
/// Đếm chi nhánh <i>đang hoạt động</i>, không đếm tất cả: BR-BRANCH-004 cho phép chi nhánh
/// đã ngừng ở lại trong dữ liệu để lịch hẹn và hóa đơn cũ vẫn đọc đúng tên, và giữ chỗ hạn
/// mức cho những bản ghi đó là phạt tiệm vì chuyện họ đã đóng cửa một điểm.
/// </para>
/// </summary>
public sealed class CreateBranchUseCase(
    IBranchRepository branches,
    BranchQuotaGuard quota,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<BranchDto> ExecuteAsync(
        CreateBranchCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        // Tới đây thì RequirePermission đã bảo đảm phiên có tiệm; phép kiểm này chỉ để lỗi
        // lập trình lộ ra sớm thay vì biến thành một chi nhánh không thuộc tiệm nào.
        var tenantId = tenantContext.ActiveTenantId
            ?? throw new TenantNotSelectedException();

        var name = Guard.Length(command.Name, "name", "Tên chi nhánh", 3, 80);

        if (await branches.NameExistsAsync(name, null, cancellationToken))
            throw DomainException.ForField("name", $"Tiệm đã có chi nhánh tên {name}.");

        // Dựng chi nhánh — tức kiểm dữ liệu nhập — trước khi xin khóa hạn mức.
        var branch = Branch.Create(
            ids.NewId("BRN"),
            tenantId,
            name,
            command.Code,
            command.Address,
            command.Phone,
            isPrimary: false,
            now);

        // Đếm hạn mức và ghi trong cùng một giao dịch — xem BranchQuotaGuard.
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await quota.EnsureRoomForOneMoreAsync(tenantId, isReactivating: false, ct);
            await branches.AddAsync(branch, ct);
            return true;
        }, cancellationToken);

        return BranchMapper.ToDto(branch);
    }
}
