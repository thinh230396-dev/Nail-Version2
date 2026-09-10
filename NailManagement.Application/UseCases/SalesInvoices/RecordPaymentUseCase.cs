using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.DTOs.Salon;
using NailManagement.Application.Mappings.Salon;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Auditing;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Policies;
using NailManagement.Domain.Repositories.Salon;

namespace NailManagement.Application.UseCases.SalesInvoices;

/// <summary>
/// Ghi nhận một lần khách trả tiền — BR-PAY-001, và là use case <b>nhiều việc nhất</b> trong
/// toàn hệ thống.
/// <para>
/// Một lần bấm "Thu tiền" ở quầy kéo theo ba thay đổi phải cùng thành công hoặc cùng không:
/// </para>
/// <list type="number">
///   <item>Thêm một dòng vào <c>InvoicePayments</c> — BR-PAY-001</item>
///   <item>Hóa đơn tính lại trạng thái từ tổng thu — BR-PAY-003, xảy ra bên trong entity</item>
///   <item>Hóa đơn vừa thu đủ thì lịch hẹn gắn với nó tự hoàn tất — BR-APT-026</item>
/// </list>
/// <para>
/// Vì sao phải là một giao dịch: hỏng giữa chừng thì hoặc tiền khách đưa biến mất khỏi hệ
/// thống, hoặc hóa đơn ghi đã thu đủ trong khi lịch hẹn vẫn treo ở "đang phục vụ" và ca sau
/// sẽ tưởng khách còn ngồi đó. Rủi ro số 3 ở §7 của lộ trình nói đúng chỗ này.
/// </para>
/// <para>
/// BR-PAY-003 — <b>không ai đặt trạng thái hóa đơn</b> ở đây. Use case chỉ thêm một dòng
/// tiền; <c>SalesInvoice.RegisterPayment</c> gọi lại công thức và tự suy ra
/// <c>PENDING</c> / <c>PARTIAL</c> / <c>PAID</c>. Đó là điều khiến một hóa đơn không bao giờ
/// nói dối về số tiền của chính nó.
/// </para>
/// </summary>
public sealed class RecordPaymentUseCase(
    ISalesInvoiceRepository invoices,
    IAppointmentRepository appointments,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<SalesInvoiceDto> ExecuteAsync(
        RecordPaymentCommand command,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var invoice = BranchScope.EnsureInScope(
            await invoices.FindByIdAsync(command.InvoiceId ?? string.Empty, cancellationToken),
            actor,
            "Không tìm thấy hóa đơn.");

        // Đọc phương thức trước khi mở giao dịch: một chuỗi sai chính tả là lỗi đầu vào, và
        // nó phải nhận về câu chữ liệt kê năm phương thức hợp lệ chứ không phải một giao dịch
        // bị cuộn ngược.
        var method = SalesInvoiceMapper.ParseMethod(command.Method);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var line = invoice.RegisterPayment(
                ids.NewId("PAY"),
                PaymentType.Payment,
                method,
                command.Amount,
                now,
                command.Reference,
                actor.UserId,
                now);

            await invoices.UpdateAsync(invoice, ct);

            await CompleteAppointmentIfSettledAsync(invoice, now, ct);

            /*
              Nhật ký ghi TRONG giao dịch, đổi lại quyết định cũ ở ngày 20.

              Lý lẽ cũ — "nằm trong giao dịch thì nó bị cuộn ngược theo khi có lỗi, và một dòng
              'đã thu tiền' cho khoản tiền chưa vào sổ còn tệ hơn không có dòng nào" — đúng cho
              việc ghi nhật ký TRƯỚC khi tiền vào sổ, nhưng không đúng cho việc ghi cùng nó:
              cuộn ngược thì cả hai cùng biến mất, nên dòng mồ côi ấy không thể tồn tại.

              Còn cái giá của việc để nhật ký ở ngoài thì có thật và nặng hơn: nếu phép ghi nhật
              ký hỏng, tiền ĐÃ vào sổ nhưng client nhận HTTP 500. Người ở quầy đọc "thất bại" và
              thu lại lần nữa — hóa đơn có hai dòng tiền cho một lần khách trả, và đó là loại sai
              phải đối soát bằng tay mới gỡ ra được.

              Đổi lại: nhật ký hỏng thì lần thu tiền hỏng theo. BR-AUD-001 xem nhật ký là bắt
              buộc với thao tác tài chính, nên "không ghi được vết thì không được thu" là câu trả
              lời đúng của hệ thống này, chứ không phải một tác dụng phụ đáng tiếc.
            */
            await audit.RecordAsync(
                new AuditEntry(
                    AuditEvent.PaymentReceived,
                    actor.UserId,
                    actor.Role,
                    invoice.TenantId,
                    nameof(SalesInvoice),
                    invoice.Id,
                    actor.Ip,
                    new Dictionary<string, string>
                    {
                        ["invoiceCode"] = invoice.Code,
                        ["method"] = SalesInvoiceMapper.ToWireFormat(line.Method),
                        ["amount"] = line.Amount.ToString(),
                        ["collected"] = invoice.Collected.ToString(),
                        ["remaining"] = invoice.Remaining.ToString(),
                        ["invoiceStatus"] = SalesInvoiceMapper.ToWireFormat(invoice.Status)
                    }),
                ct);

            return line;
        }, cancellationToken);

        return SalesInvoiceMapper.ToDto(invoice);
    }

    /// <summary>
    /// BR-APT-026 — hóa đơn vừa thu đủ thì lịch hẹn gắn với nó tự hoàn tất.
    /// <para>
    /// Ba lớp bỏ qua, mỗi lớp có lý do riêng:
    /// </para>
    /// <list type="bullet">
    ///   <item>Chưa thu đủ — lịch còn đang phục vụ, chưa có gì để đóng</item>
    ///   <item>Hóa đơn bán lẻ (BR-INV-011) — không có lịch hẹn nào để đóng</item>
    ///   <item>
    ///     Lịch đang ở một trạng thái không hoàn tất được — đã hủy, hoặc khách không đến. Bỏ
    ///     qua <b>trong im lặng</b> thay vì ném lỗi: BR-APT-041 nói ba trạng thái cuối không
    ///     quay lại được, nên ép nó thành "hoàn tất" là ghi đè một quyết định người ở quầy đã
    ///     chủ động đưa ra. Và quan trọng hơn: lần thu tiền không được thất bại vì lịch hẹn
    ///     nằm ở đâu — tiền khách đưa là có thật dù buổi hẹn đã hủy.
    ///   </item>
    /// </list>
    /// </summary>
    private async Task CompleteAppointmentIfSettledAsync(
        SalesInvoice invoice, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!invoice.IsFullyPaid() || string.IsNullOrWhiteSpace(invoice.AppointmentId)) return;

        var appointment = await appointments.FindByIdAsync(invoice.AppointmentId, cancellationToken);

        if (appointment is null) return;

        if (!AppointmentLifecyclePolicy.CanCompleteFromPayment(appointment.Status)) return;

        appointment.CompleteFromPaidInvoice(now);

        await appointments.UpdateAsync(appointment, cancellationToken);
    }
}
