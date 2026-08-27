using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Policies;
using NailManagement.Domain.Repositories;
using AppointmentEntity = NailManagement.Domain.Entities.Salon.Appointment;

namespace NailManagement.Application.UseCases.Appointments;

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
        var staff = await guard.ResolveStaffAsync(command.StaffId, actor, cancellationToken);
        var services = await guard.ResolveServicesAsync(command.Services, cancellationToken);

        // BR-APT-010 — giờ kết thúc phải tính TRƯỚC phép chống trùng, vì chính nó là vế thứ
        // hai của phép so khoảng. Dùng đúng hàm mà entity sẽ dùng lại ngay bên dưới, để khoảng
        // được kiểm và khoảng được lưu không thể lệch nhau.
        var end = command.StartAt.AddMinutes(AppointmentSchedulePolicy.TotalMinutes(
            services.Select(service => (service.DurationMinutes, service.BufferMinutes))));

        // BR-APT-011 — chặn cứng. Giữa phép kiểm này và lệnh ghi bên dưới vẫn còn một khe hở
        // lý thuyết cho hai request đặt cùng giờ cho cùng một người trong cùng một khoảnh khắc.
        // Không xử lý bằng khóa hay mức cô lập chặt hơn là quyết định có chủ đích: một tiệm có
        // một quầy lễ tân, và §9.4 đã loại mọi hạ tầng đồng thời khỏi phạm vi MVP.
        var warnings = await guard.EnsureSlotAvailableAsync(
            staff, command.StartAt, end, null, now, cancellationToken);

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

        await appointments.AddAsync(appointment, cancellationToken);

        // Dựng DTO từ hai bản ghi đã nạp sẵn ở trên thay vì đọc lại lịch hẹn vừa ghi: một câu
        // truy vấn nữa chỉ để lấy lại đúng những thứ đang nằm trong tay.
        return new AppointmentSaveResult(
            AppointmentMapper.ToDto(appointment, customer, staff, now), warnings);
    }
}
