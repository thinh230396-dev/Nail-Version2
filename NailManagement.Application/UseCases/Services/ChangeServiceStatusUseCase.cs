using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Services;

/// <summary>
/// Ngừng bán hoặc bán lại một dịch vụ — BR-SVC-005.
/// <para>
/// Đây chính là thứ mà giao diện gọi là "xóa dịch vụ". Không có lệnh xóa cứng nào
/// (BR-DEL-001): dịch vụ đã ngừng vẫn ở lại để lịch hẹn và hóa đơn cũ đọc đúng tên
/// (BR-DEL-003), nó chỉ không đặt lịch mới được nữa.
/// </para>
/// <para>
/// Khác <c>ChangeBranchStatusUseCase</c> ở chỗ bật lại <b>không phải qua hạn mức nào</b>:
/// BR-SUB-005 không đặt hạn mức cho số dịch vụ, nên ở đây không có gì để đếm.
/// </para>
/// </summary>
public sealed class ChangeServiceStatusUseCase(
    IServiceRepository services,
    IClock clock)
{
    public async Task<ServiceDto> ExecuteAsync(
        ChangeServiceStatusCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var status = ServiceMapper.ParseStatus(command.Status);

        var service = await services.FindByIdAsync(command.ServiceId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy dịch vụ.");

        if (status == ServiceStatus.Inactive) service.Deactivate(now);
        else service.Activate(now);

        await services.UpdateAsync(service, cancellationToken);

        return ServiceMapper.ToDto(service);
    }
}
