using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Common;
using NailManagement.Domain.Policies;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Appointments;

/// <summary>
/// BR-APT-025 — dời lịch: sửa giờ bắt đầu, giữ nguyên trạng thái và giữ nguyên độ dài.
/// <para>
/// Có đường riêng thay vì bắt gọi lệnh sửa trọn, vì đây là thao tác kéo một ô trên bảng giờ.
/// Gửi trọn hồ sơ chỉ để đổi mỗi giờ bắt đầu là mở ra khả năng ghi đè nhầm những trường mà
/// người dùng không hề chạm tới — và trên một màn hình kéo thả thì không có biểu mẫu nào để
/// mà đọc lại giá trị trước khi gửi.
/// </para>
/// <para>
/// Phạm vi hẹp hơn lệnh sửa trọn: chỉ dời được lịch còn ở <c>PENDING</c> hoặc <c>CONFIRMED</c>.
/// Khách đã đến quầy rồi thì việc cần làm là đổi trạng thái, không phải đẩy giờ hẹn đi chỗ khác.
/// </para>
/// </summary>
public sealed class RescheduleAppointmentUseCase(
    IAppointmentRepository appointments,
    AppointmentBookingGuard guard,
    IClock clock)
{
    public async Task<AppointmentSaveResult> ExecuteAsync(
        RescheduleAppointmentCommand command,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var appointment = BranchScope.EnsureInScope(
            await appointments.FindByIdAsync(command.AppointmentId ?? string.Empty, cancellationToken),
            actor,
            "Không tìm thấy lịch hẹn.");

        // Kiểm trạng thái TRƯỚC phép chống trùng, dù Appointment.Reschedule cũng kiểm lại lần
        // nữa. Không phải để phòng hờ, mà để câu trả lời đúng trọng tâm: một lịch đã hoàn tất
        // thì lý do bị từ chối là nó đã hoàn tất, chứ không phải khung giờ mới có ai đó chiếm.
        if (!AppointmentLifecyclePolicy.CanReschedule(appointment.Status))
            throw DomainException.ForField("startAt", "Chỉ dời được lịch hẹn đang chờ hoặc đã xác nhận.");

        var staff = appointment.Staff ?? throw new InvalidOperationException(
            $"Lịch hẹn {appointment.Id} được đọc mà chưa nạp kèm Staff. "
            + "Truy vấn ở tầng lưu trữ phải Include bảng này trước khi kiểm ca làm việc.");

        // Giữ nguyên độ dài: dời lịch không đụng tới danh sách dịch vụ, nên khoảng chiếm chỗ
        // vẫn đúng bằng khoảng cũ (BR-APT-010).
        var end = command.StartAt.AddMinutes(appointment.TotalMinutes());

        var warnings = await guard.EnsureSlotAvailableAsync(
            staff, command.StartAt, end, appointment.Id, now, cancellationToken);

        appointment.Reschedule(command.StartAt, now);

        await appointments.UpdateAsync(appointment, cancellationToken);

        return new AppointmentSaveResult(AppointmentMapper.ToDto(appointment, now), warnings);
    }
}
