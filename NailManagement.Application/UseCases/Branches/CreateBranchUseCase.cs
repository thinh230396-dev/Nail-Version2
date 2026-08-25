using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Branches;

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
    ITenantRepository tenants,
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

        var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tiệm đang làm việc.");

        if (tenant.Package is null)
            throw new InvalidOperationException(
                $"Tiệm {tenant.Id} không đọc được gói đăng ký. Kho dữ liệu phải trả về tiệm kèm gói.");

        var name = Guard.Length(command.Name, "name", "Tên chi nhánh", 3, 80);

        if (await branches.NameExistsAsync(name, null, cancellationToken))
            throw DomainException.ForField("name", $"Tiệm đã có chi nhánh tên {name}.");

        var activeCount = await branches.CountActiveAsync(cancellationToken);

        if (activeCount >= tenant.Package.MaxSalons)
        {
            throw new LimitExceededException(
                $"Gói {tenant.Package.Name} chỉ cho phép {tenant.Package.MaxSalons} chi nhánh đang hoạt động. "
                + "Nâng gói để thêm chi nhánh mới.");
        }

        var branch = Branch.Create(
            ids.NewId("BRN"),
            tenantId,
            name,
            command.Code,
            command.Address,
            command.Phone,
            isPrimary: false,
            now);

        await branches.AddAsync(branch, cancellationToken);

        return BranchMapper.ToDto(branch);
    }
}
