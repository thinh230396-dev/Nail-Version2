using NailManagement.Application.Abstractions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Appointments;

/// <summary>
/// BR-APT-022 — chuyển trạng thái lịch hẹn theo đúng sơ đồ ở mục 16.1 của tài liệu nghiệp vụ.
/// <para>
/// Use case này cố ý <b>mỏng</b>: toàn bộ luật nằm ở <c>AppointmentLifecyclePolicy</c>, viết
/// thành một bảng dữ liệu, và entity là nơi hỏi bảng đó. Chép lại vài nhánh điều kiện ở đây là
/// dựng chỗ cho hai câu trả lời khác nhau về cùng một câu hỏi.
/// </para>
/// <para>
/// <b>Không chuyển sang <c>COMPLETED</c> qua đường này.</b> BR-APT-026 quy định lịch chỉ hoàn
/// tất tự động khi hóa đơn gắn với nó chuyển sang đã thanh toán, và BR-APT-027 cho chủ tiệm
/// một ngoại lệ khi hóa đơn còn thiếu tiền. Cả hai đều cần bảng hóa đơn bán hàng, thứ mà lát
/// cắt thu tiền mới dựng, nên hôm nay <c>Appointment.ChangeStatus</c> từ chối thẳng mọi lần
/// chuyển tới <c>COMPLETED</c> — kể cả của chủ tiệm. Ô quyền <c>ForceCompleteAppointment</c>
/// đã có sẵn trong ma trận từ ngày 3 và sẽ được dùng tới ở lát cắt đó.
/// </para>
/// <para>
/// BR-APT-024 — không có lệnh xóa lịch hẹn. Hủy lịch chính là chuyển sang <c>CANCELLED</c> qua
/// đúng endpoint này, và BR-APT-032 nói rõ tiền cọc của một lịch bị hủy không được hệ thống
/// tự xử lý: nó nằm nguyên tại chỗ để người ở quầy thỏa thuận với khách.
/// </para>
/// </summary>
public sealed class ChangeAppointmentStatusUseCase(
    IAppointmentRepository appointments,
    IClock clock)
{
    public async Task<AppointmentDto> ExecuteAsync(
        ChangeAppointmentStatusCommand command,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var appointment = AppointmentScope.EnsureInScope(
            await appointments.FindByIdAsync(command.AppointmentId ?? string.Empty, cancellationToken),
            actor);

        appointment.ChangeStatus(AppointmentMapper.ParseStatus(command.Status), now);

        await appointments.UpdateAsync(appointment, cancellationToken);

        return AppointmentMapper.ToDto(appointment, now);
    }
}
