using NailManagement.Application.DTOs.Salon;
using NailManagement.Application.Mappings.Salon;
using NailManagement.Domain.Repositories.Salon;

namespace NailManagement.Application.UseCases.Services;

/// <summary>
/// Bảng dịch vụ của tiệm đang làm việc — BR-SVC-001.
/// <para>
/// Không nhận tham số nào, kể cả mã tiệm hay mã chi nhánh. Tiệm đến từ phiên đăng nhập
/// (BR-AUTH-024) và bộ lọc toàn cục ở tầng lưu trữ áp nó vào truy vấn (BR-ISO-002); còn chi
/// nhánh thì không liên quan — BR-SVC-001 quy định dịch vụ dùng chung cho mọi chi nhánh của
/// tiệm, nên lễ tân và chủ tiệm nhìn thấy đúng một bảng giá.
/// </para>
/// </summary>
public sealed class ListServicesUseCase(IServiceRepository services)
{
    public async Task<IReadOnlyList<ServiceDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var all = await services.ListAsync(cancellationToken);

        return [.. all.Select(ServiceMapper.ToDto)];
    }
}
