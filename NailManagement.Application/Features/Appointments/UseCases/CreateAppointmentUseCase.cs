using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Appointments;
using NailManagement.Domain.Salon.Appointments;

using AppointmentEntity = NailManagement.Domain.Salon.Appointments.Appointment;

namespace NailManagement.Application.Features.Appointments.UseCases;

/// <summary>
/// Đặt một lịch hẹn — BR-APT-001, lễ tân và chủ tiệm làm được, Superadmin thì không
/// (BR-AUTH-030, cưỡng chế ở ma trận quyền).
/// <para>
/// Chi nhánh của lịch hẹn <b>suy ra từ kỹ thuật viên</b> chứ không nhận từ thân request.
/// BR-EMP-003 cho mỗi nhân viên đúng một chi nhánh, nên hai giá trị ấy vốn là một sự thật;
/// nhận cả hai là dựng ra chỗ để chúng nói khác nhau, và bộ nạp dữ liệu mẫu ở
/// <c>DemoDataSeeder</c> cũng đã lấy chi nhánh theo đúng cách này từ ngày 2.
/// </para>
/// <para>
/// Không ghi nhật ký kiểm toán: BR-AUD-002 liệt kê đúng tám loại sự kiện được ghi, và đặt
/// lịch không nằm trong đó. Ghi thêm là biến nhật ký kiểm toán thành nhật ký thao tác, thứ mà
/// tài liệu nghiệp vụ cố ý không làm.
/// </para>
/// </summary>
public sealed class CreateAppointmentUseCase(
    IAppointmentRepository appointments,
    AppointmentBookingGuard guard,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<AppointmentSaveResult> ExecuteAsync(
        CreateAppointmentCommand command,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var tenantId = tenantContext.ActiveTenantId
            ?? throw new TenantNotSelectedException();

        var customer = await guard.ResolveCustomerAsync(command.CustomerId, cancellationToken);
        var staff = await guard.ResolveStaffForNewAppointmentAsync(command.StaffId, actor, cancellationToken);
        var services = await guard.ResolveServicesAsync(command.Services, cancellationToken);

        // BR-APT-010 — giờ kết thúc phải tính TRƯỚC phép chống trùng, vì chính nó là vế thứ
        // hai của phép so khoảng. Dùng đúng hàm mà entity sẽ dùng lại ngay bên dưới, để khoảng
        // được kiểm và khoảng được lưu không thể lệch nhau.
        var end = command.StartAt.AddMinutes(AppointmentSchedulePolicy.TotalMinutes(
            services.Select(service => (service.DurationMinutes, service.BufferMinutes))));

        // BR-APT-011 — chặn cứng, trong CÙNG giao dịch với lệnh ghi. Guard khóa lịch của kỹ thuật
        // viên trước khi kiểm, và khóa chỉ nhả khi giao dịch này kết thúc — lúc lịch mới đã nằm
        // trong bảng. Nhiều quầy đặt cùng người cùng giờ thì chỉ quầy đầu tiên qua được.
        //
        // (Bản MVP cố ý bỏ khóa, với lý do "một tiệm một quầy lễ tân". Nhiều chi nhánh, nhiều
        // lễ tân và nguồn đặt lịch ONLINE làm lý do ấy không còn đứng vững — phép thử
        // DoubleBookingTests từng thấy sáu quầy cùng đặt thành công một khung giờ.)
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var warnings = await guard.EnsureSlotAvailableAsync(
                staff, command.StartAt, end, null, now, ct);

            var appointment = AppointmentEntity.Create(
                ids.NewId("APT"),
                tenantId,
                staff.BranchId,
                customer.Id,
                staff.Id,
                command.StartAt,
                services,
                AppointmentMapper.ParseInitialStatus(command.Status),
                AppointmentMapper.ParseSource(command.Source),
                command.Station,
                command.Note,
                command.Deposit,
                actor.UserId,
                now);

            await appointments.AddAsync(appointment, ct);

            // Dựng DTO từ hai bản ghi đã nạp sẵn ở trên thay vì đọc lại lịch hẹn vừa ghi: một câu
            // truy vấn nữa chỉ để lấy lại đúng những thứ đang nằm trong tay.
            return new AppointmentSaveResult(
                AppointmentMapper.ToDto(appointment, customer, staff, now), warnings);
        }, cancellationToken);
    }
}
