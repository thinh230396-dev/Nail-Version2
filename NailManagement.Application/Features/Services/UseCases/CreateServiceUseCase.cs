using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Services;
using NailManagement.Domain.Common;
using NailManagement.Domain.Policies;
using NailManagement.Domain.Repositories.Salon;
using ServiceEntity = NailManagement.Domain.Entities.Salon.Service;

namespace NailManagement.Application.Features.Services.UseCases;

/// <summary>
/// Thêm một dịch vụ vào bảng giá của tiệm đang làm việc.
/// <para>
/// Không có hạn mức nào cho số dịch vụ: BR-SUB-005 chỉ cưỡng chế hai hạn mức thật là
/// <c>max_salons</c> và <c>max_staff</c>. Đó là lý do use case này ngắn hơn hẳn
/// <c>CreateBranchUseCase</c> dù hai việc trông giống nhau.
/// </para>
/// <para>
/// Mọi phép kiểm tra về giá, thời lượng và thời gian dọn dẹp nằm trong <c>Service.Create</c>
/// chứ không ở đây — chúng luôn đúng bất kể ai gọi tới, kể cả bộ nạp dữ liệu mẫu.
/// </para>
/// <para>
/// Tên lớp thực thể được đặt bí danh <c>ServiceEntity</c> vì thư mục lát cắt này tên
/// <c>Services</c>: không có bí danh thì <c>Service</c> trần trở thành một cái tên mà trình
/// biên dịch phải đoán giữa thực thể và tên vùng.
/// </para>
/// </summary>
public sealed class CreateServiceUseCase(
    IServiceRepository services,
    ITenantContext tenantContext,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<ServiceDto> ExecuteAsync(
        CreateServiceCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        // Tới đây thì RequirePermission đã bảo đảm phiên có tiệm; phép kiểm này chỉ để lỗi
        // lập trình lộ ra sớm thay vì biến thành một dịch vụ không thuộc tiệm nào.
        var tenantId = tenantContext.ActiveTenantId
            ?? throw new TenantNotSelectedException();

        // Cắt khoảng trắng trước khi so trùng tên. Entity vẫn kiểm lại lần nữa bằng chính
        // hằng số này, nên hai phép kiểm không thể nói hai điều khác nhau.
        var name = Guard.NotEmpty(command.Name, "name", "Tên dịch vụ", ValidationPolicy.NameMaxLength);

        // BR-VAL-001 — tên dịch vụ duy nhất trong một tiệm. Kiểm ở đây thay vì để chỉ số duy
        // nhất của database từ chối, vì lỗi ràng buộc thô không nói được ô nhập nào sai.
        if (await services.NameExistsAsync(name, null, cancellationToken))
            throw DomainException.ForField("name", $"Tiệm đã có dịch vụ tên {name}.");

        var service = ServiceEntity.Create(
            ids.NewId("SVC"),
            tenantId,
            name,
            command.Category,
            command.Price,
            command.DurationMinutes,
            command.BufferMinutes,
            command.Description,
            now);

        await services.AddAsync(service, cancellationToken);

        return ServiceMapper.ToDto(service);
    }
}
