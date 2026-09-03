using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Policies;
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
/// <b>Chỉ có đúng một ngoại lệ</b>, và nó là <c>COMPLETED</c>. Hai luật quyết định ai đóng
/// được một lịch hẹn:
/// </para>
/// <list type="bullet">
///   <item>
///     BR-APT-026 — đường thường: lịch tự hoàn tất khi hóa đơn thu đủ tiền. Đường đó nằm ở
///     <c>RecordPaymentUseCase</c>, không ai bấm nút để nó xảy ra.
///   </item>
///   <item>
///     BR-APT-027 — ngoại lệ: <b>chỉ chủ tiệm</b> đóng tay được một lịch đang phục vụ dở khi
///     hóa đơn chưa thu đủ. Đó là đường đi qua đây.
///   </item>
/// </list>
/// <para>
/// BR-APT-024 — không có lệnh xóa lịch hẹn. Hủy lịch chính là chuyển sang <c>CANCELLED</c> qua
/// đúng endpoint này, và BR-APT-032 nói rõ tiền cọc của một lịch bị hủy không được hệ thống
/// tự xử lý: nó nằm nguyên tại chỗ để người ở quầy thỏa thuận với khách.
/// </para>
/// </summary>
public sealed class ChangeAppointmentStatusUseCase(
    IAppointmentRepository appointments,
    ISalesInvoiceRepository invoices,
    IClock clock)
{
    public async Task<AppointmentDto> ExecuteAsync(
        ChangeAppointmentStatusCommand command,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var appointment = BranchScope.EnsureInScope(
            await appointments.FindByIdAsync(command.AppointmentId ?? string.Empty, cancellationToken),
            actor,
            "Không tìm thấy lịch hẹn.");

        var target = AppointmentMapper.ParseStatus(command.Status);

        if (target == AppointmentStatus.Completed)
            await ForceCompleteAsync(appointment, actor, now, cancellationToken);
        else
            appointment.ChangeStatus(target, now);

        await appointments.UpdateAsync(appointment, cancellationToken);

        return AppointmentMapper.ToDto(appointment, now);
    }

    /// <summary>
    /// BR-APT-027 — chủ tiệm đóng tay một lịch đang phục vụ dở khi hóa đơn chưa thu đủ.
    /// <para>
    /// Phép kiểm quyền nằm ở <b>đây</b> chứ không ở bộ lọc <c>RequirePermission</c> của
    /// endpoint, vì nó phụ thuộc vào <i>nội dung</i> request: cùng một đường dẫn
    /// <c>PATCH /{id}/status</c> phục vụ cả việc hủy lịch — thứ lễ tân làm cả ngày — lẫn ngoại
    /// lệ này. Một bộ lọc chạy trước khi thân request được đọc thì không phân biệt được hai
    /// việc đó, nên gắn nhóm <c>ForceCompleteAppointment</c> lên cả endpoint sẽ khóa luôn
    /// đường hủy lịch của lễ tân.
    /// </para>
    /// <para>
    /// <b>Không đòi phải có hóa đơn</b> — quyết định 58 chốt ngày 13. BR-APT-027 chỉ nói tới
    /// ca hóa đơn còn <c>PARTIAL</c>, nhưng ca tệ hơn hẳn là khách bỏ về giữa chừng không trả
    /// đồng nào: khi đó lịch chưa có hóa đơn, và BR-APT-040 lại cấm hủy một lịch đang phục vụ.
    /// Đòi phải có hóa đơn là để lịch ấy kẹt vĩnh viễn ở "đang phục vụ", và bảng lịch của tiệm
    /// sẽ mang theo một dòng không ai đóng được.
    /// </para>
    /// </summary>
    private async Task ForceCompleteAsync(
        Appointment appointment,
        ActorContext actor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!PermissionMatrix.Allows(actor.Role, Feature.ForceCompleteAppointment, isWrite: true))
        {
            throw new ForbiddenException(
                "Lịch hẹn tự hoàn tất khi hóa đơn thu đủ tiền. "
                + "Đóng lịch khi chưa thu đủ là quyền của chủ tiệm.");
        }

        var invoice = await invoices.FindOpenByAppointmentAsync(appointment.Id, cancellationToken);

        // Hóa đơn đã thu đủ mà lịch vẫn chưa đóng là một trạng thái lẽ ra không xảy ra —
        // BR-APT-026 đóng nó ngay lúc thu. Gặp thì vẫn đóng, nhưng KHÔNG bật cờ "hoàn tất khi
        // chưa thu đủ": cờ ấy đi vào báo cáo và vào mắt chủ tiệm, nên gắn nhầm nó lên một buổi
        // đã trả đủ tiền là vu cho khách một khoản nợ không có thật.
        if (invoice is not null && invoice.IsFullyPaid())
        {
            appointment.CompleteFromPaidInvoice(now);
            return;
        }

        appointment.CompleteWithUnpaidBalance(now);
    }
}
