using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Common;
using NailManagement.Domain.Policies;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Services;

/// <summary>
/// Sửa một dịch vụ, kể cả đổi giá.
/// <para>
/// <b>Đổi giá không cần thận trọng gì đặc biệt ở đây</b>, và đó là hệ quả trực tiếp của
/// BR-SVC-006: dòng hóa đơn lưu <c>unit_price</c> của chính nó tại thời điểm lập, không
/// tham chiếu ngược sang bảng này. Nhờ vậy sửa giá hôm nay không đụng tới một đồng nào
/// trong sổ sách đã chốt, và hệ thống không cần bảng lịch sử giá.
/// </para>
/// <para>
/// Dịch vụ của tiệm khác không tìm thấy được ở đây, và câu trả lời là <c>NOT_FOUND</c> chứ
/// không phải <c>FORBIDDEN</c> — BR-TENANT-013 bước 4.
/// </para>
/// </summary>
public sealed class UpdateServiceUseCase(
    IServiceRepository services,
    IClock clock)
{
    public async Task<ServiceDto> ExecuteAsync(
        UpdateServiceCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var service = await services.FindByIdAsync(command.ServiceId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy dịch vụ.");

        var name = Guard.NotEmpty(command.Name, "name", "Tên dịch vụ", ValidationPolicy.NameMaxLength);

        // Bỏ qua chính nó khi so trùng tên, nếu không thì lưu lại mà không đổi tên cũng hỏng.
        if (await services.NameExistsAsync(name, service.Id, cancellationToken))
            throw DomainException.ForField("name", $"Tiệm đã có dịch vụ tên {name}.");

        service.UpdateDetails(
            name,
            command.Category,
            command.Price,
            command.DurationMinutes,
            command.BufferMinutes,
            command.Description,
            now);

        await services.UpdateAsync(service, cancellationToken);

        return ServiceMapper.ToDto(service);
    }
}
