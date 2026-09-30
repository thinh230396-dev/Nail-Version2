using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Features.Appointments;
using NailManagement.Domain.Salon.Appointments;
using NailManagement.Domain.Shared;

namespace NailManagement.Application.Features.Appointments.UseCases;

/// <summary>
/// Sửa trọn một lịch hẹn: đổi khách, đổi kỹ thuật viên, đổi giờ, đổi danh sách dịch vụ, đổi
/// ghế, ghi chú, tiền cọc và nguồn.
/// <para>
/// Chạy lại <b>đúng bộ kiểm tra của lệnh đặt mới</b>, không phải một bộ rút gọn: đổi dịch vụ
/// là đổi độ dài buổi hẹn (BR-APT-010), đổi kỹ thuật viên là đổi cả người lẫn chi nhánh, nên
/// một lệnh sửa có thể tạo ra trùng lịch y hệt một lệnh đặt mới. Đó là lý do cả hai đường đi
/// qua cùng một <see cref="AppointmentBookingGuard"/>.
/// </para>
/// <para>
/// Phép chống trùng bỏ qua chính lịch hẹn đang sửa. Thiếu điều đó thì một lần bấm Lưu mà
/// không đổi gì cũng bị chính nó báo là trùng giờ.
/// </para>
/// </summary>
public sealed class UpdateAppointmentUseCase(
    IAppointmentRepository appointments,
    AppointmentBookingGuard guard,
    IUnitOfWork unitOfWork,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<AppointmentSaveResult> ExecuteAsync(
        UpdateAppointmentCommand command,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var appointment = BranchScope.EnsureInScope(
            await appointments.FindByIdAsync(command.AppointmentId ?? string.Empty, cancellationToken),
            actor,
            "Không tìm thấy lịch hẹn.");

        // Kiểm trạng thái TRƯỚC mọi phép kiểm khác, dù Appointment.Revise cũng kiểm lại lần
        // nữa. Không phải để phòng hờ, mà để câu trả lời đúng trọng tâm: sửa một lịch đã hủy
        // thì lý do bị từ chối là nó đã hủy, chứ không phải khung giờ cũ của nó nay có người
        // khác chiếm — và khung giờ ấy trống ra được chính là vì nó đã bị hủy.
        if (AppointmentLifecyclePolicy.IsFinal(appointment.Status))
        {
            throw DomainException.ForField(
                "status",
                $"Lịch hẹn ở trạng thái “{AppointmentStatusText.Label(appointment.Status)}” "
                + "thì không sửa được nữa.");
        }

        var customer = await guard.ResolveCustomerAsync(command.CustomerId, cancellationToken);
        var staff = await guard.ResolveStaffAsync(command.StaffId, actor, cancellationToken);
        var services = await guard.ResolveServicesAsync(command.Services, cancellationToken);

        var end = command.StartAt.AddMinutes(AppointmentSchedulePolicy.TotalMinutes(
            services.Select(service => (service.DurationMinutes, service.BufferMinutes))));

        // Kiểm trùng và ghi trong cùng một giao dịch — xem CreateAppointmentUseCase.
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var warnings = await guard.EnsureSlotAvailableAsync(
                staff, command.StartAt, end, appointment.Id, now, ct);

            // Chi nhánh đi theo kỹ thuật viên, giống hệt lúc đặt mới (BR-EMP-003). Đổi người làm
            // sang chi nhánh khác là chuyển luôn lịch hẹn sang chi nhánh đó — và với lễ tân thì
            // không xảy ra được, vì AppointmentBookingGuard đã chặn ở bước chọn người.
            appointment.Revise(
                staff.BranchId,
                customer.Id,
                staff.Id,
                command.StartAt,
                services,
                AppointmentMapper.ParseSource(command.Source),
                command.Station,
                command.Note,
                command.Deposit,
                now,
                () => ids.NewId("APS"));

            await appointments.UpdateAsync(appointment, ct);

            return new AppointmentSaveResult(
                AppointmentMapper.ToDto(appointment, customer, staff, now), warnings);
        }, cancellationToken);
    }
}
