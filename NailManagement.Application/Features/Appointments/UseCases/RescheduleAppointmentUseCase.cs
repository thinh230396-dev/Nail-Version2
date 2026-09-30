using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Features.Appointments;
using NailManagement.Domain.Salon.Appointments;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.Shared;

namespace NailManagement.Application.Features.Appointments.UseCases;

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
    IUnitOfWork unitOfWork,
    IStaffRepository staffMembers,
    AppointmentReadService reader,
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

        // Khóa ngoại ghép bảo đảm kỹ thuật viên tồn tại trong cùng tiệm; không thấy nghĩa là dữ
        // liệu hỏng, không phải lỗi người dùng.
        var staff = await staffMembers.FindByIdAsync(appointment.StaffId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Lịch hẹn {appointment.Id} trỏ tới kỹ thuật viên {appointment.StaffId} nhưng không đọc được hồ sơ.");

        // Giữ nguyên độ dài: dời lịch không đụng tới danh sách dịch vụ, nên khoảng chiếm chỗ
        // vẫn đúng bằng khoảng cũ (BR-APT-010).
        var end = command.StartAt.AddMinutes(appointment.TotalMinutes());

        // Kiểm trùng và ghi trong cùng một giao dịch — xem CreateAppointmentUseCase.
        var warnings = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var found = await guard.EnsureSlotAvailableAsync(
                staff, command.StartAt, end, appointment.Id, now, ct);

            appointment.Reschedule(command.StartAt, now);

            await appointments.UpdateAsync(appointment, ct);

            return found;
        }, cancellationToken);

        return new AppointmentSaveResult(
            await reader.DescribeAsync(appointment, now, cancellationToken), warnings);
    }
}
