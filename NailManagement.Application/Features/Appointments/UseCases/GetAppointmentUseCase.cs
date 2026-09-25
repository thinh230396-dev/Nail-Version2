using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Features.Appointments;
using NailManagement.Domain.Repositories.Salon;

namespace NailManagement.Application.Features.Appointments.UseCases;

/// <summary>
/// Một lịch hẹn, cho ngăn chi tiết và cho màn hình vừa dời lịch xong muốn đọc lại.
/// <para>
/// Trả về đúng hình dạng <see cref="AppointmentDto"/> của danh sách, không có bản "chi tiết"
/// giàu hơn — khác <c>GET /api/customers/{id}</c> ở ngày 10, thứ có thêm lịch sử ghé tiệm.
/// Lý do: một lịch hẹn đã mang sẵn trọn vẹn nội dung của nó ngay trong dòng danh sách, gồm cả
/// các dòng dịch vụ. Dựng một DTO thứ hai giống hệt DTO thứ nhất chỉ để có chỗ đặt tên là
/// dựng thêm một thứ phải giữ cho khớp.
/// </para>
/// </summary>
public sealed class GetAppointmentUseCase(IAppointmentRepository appointments, IClock clock)
{
    public async Task<AppointmentDto> ExecuteAsync(
        string id, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var appointment = BranchScope.EnsureInScope(
            await appointments.FindByIdAsync(id ?? string.Empty, cancellationToken),
            actor,
            "Không tìm thấy lịch hẹn.");

        return AppointmentMapper.ToDto(appointment, clock.UtcNow);
    }
}
