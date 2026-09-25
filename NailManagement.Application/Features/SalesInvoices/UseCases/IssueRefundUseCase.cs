using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Features.SalesInvoices;
using NailManagement.Domain.Auditing;
using NailManagement.Domain.Salon.Invoices;

namespace NailManagement.Application.Features.SalesInvoices.UseCases;

/// <summary>
/// Hoàn tiền cho khách — BR-PAY-006, một dòng thu mang số <b>âm</b> kèm lý do bắt buộc.
/// <para>
/// Không sửa và không xóa dòng thu cũ. Giữ đủ cả hai chiều tiền là điều làm cho sổ sách đối
/// chiếu được, và nhờ dấu âm mà công thức doanh thu ở BR-REV-001 chỉ cần cộng dồn là đã tự
/// trừ phần đã trả lại — không cần một nhánh riêng cho hoàn tiền ở tầng báo cáo.
/// </para>
/// <para>
/// Ba ràng buộc, cả ba đều cưỡng chế ở tầng Domain trong <c>SalesInvoice.IssueRefund</c>:
/// </para>
/// <list type="bullet">
///   <item>Chỉ hoàn được hóa đơn <b>đã thanh toán đủ</b> — sơ đồ mục 16.2 chỉ có mũi tên <c>PAID → REFUNDED</c></item>
///   <item>BR-PAY-008 — tổng hoàn không vượt tổng đã thu</item>
///   <item>BR-PAY-006 — lý do bắt buộc, vì đây là thứ duy nhất giải thích được vì sao két thiếu tiền</item>
/// </list>
/// <para>
/// <c>REFUNDED</c> là <b>điểm cuối</b> theo mục 16.2, nên mỗi hóa đơn chỉ hoàn được một lần.
/// Hoàn một phần rồi muốn hoàn tiếp thì không có đường — đó là chủ đích của sơ đồ, không phải
/// thiếu sót: một chứng từ đã đóng thì mọi điều chỉnh sau đó thuộc về sổ sách bên ngoài.
/// </para>
/// <para>
/// BR-PAY-007 — chỉ chủ tiệm. Phép kiểm tra nằm ở bộ lọc <c>RequirePermission</c> với nhóm
/// <c>Refunds</c>, ô mà lễ tân cố ý vắng mặt trong ma trận. Lễ tân thu tiền cả ngày nhưng
/// không được trả tiền ra khỏi két.
/// </para>
/// <para>
/// <b>Không đụng tới lịch hẹn.</b> BR-APT-020 đã bỏ hẳn trạng thái <c>REFUNDED</c> khỏi lịch
/// hẹn vì hoàn tiền là chuyện của hóa đơn, và BR-APT-041 nói <c>COMPLETED</c> không quay lại
/// được. Buổi làm đã diễn ra thật; trả lại tiền không xóa được điều đó.
/// </para>
/// </summary>
public sealed class IssueRefundUseCase(
    ISalesInvoiceRepository invoices,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<SalesInvoiceDto> ExecuteAsync(
        IssueRefundCommand command,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var invoice = BranchScope.EnsureInScope(
            await invoices.FindByIdAsync(command.InvoiceId ?? string.Empty, cancellationToken),
            actor,
            "Không tìm thấy hóa đơn.");

        var method = SalesInvoiceMapper.ParseMethod(command.Method);

        /*
          Một giao dịch bao quanh, dù chỉ chạm đúng một gốc tổng hợp.

          Trước ngày 24 ở đây không có giao dịch nào, với lý do "hoàn tiền chỉ chạm một gốc tổng
          hợp nên một lệnh lưu là đã trọn vẹn". Câu ấy đúng về phía dữ liệu nghiệp vụ, nhưng bỏ
          sót lệnh ghi thứ hai: dòng nhật ký kiểm toán. Hai lệnh lưu rời nhau nghĩa là tiền có
          thể đã hoàn xong trong khi lời gọi trả về HTTP 500 — và người ở quầy sẽ hoàn lần nữa.

          Xem chú thích dài ở RecordPaymentUseCase để biết vì sao đổi hướng: cùng một lập luận,
          cùng một cái giá phải trả.
        */
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var refund = invoice.IssueRefund(
                ids.NewId("PAY"),
                method,
                command.Amount,
                now,
                command.Reason ?? string.Empty,
                actor.UserId,
                now);

            await invoices.UpdateAsync(invoice, ct);

            await audit.RecordAsync(
                new AuditEntry(
                    AuditEvent.RefundIssued,
                    actor.UserId,
                    actor.Role,
                    invoice.TenantId,
                    nameof(SalesInvoice),
                    invoice.Id,
                    actor.Ip,
                    new Dictionary<string, string>
                    {
                        ["invoiceCode"] = invoice.Code,
                        ["method"] = SalesInvoiceMapper.ToWireFormat(refund.Method),

                        // Ghi trị tuyệt đối chứ không ghi dấu âm của dòng tiền: người đọc nhật ký
                        // hỏi "hoàn bao nhiêu", còn dấu âm là quy ước của bảng thu tiền để công
                        // thức doanh thu cộng dồn được, không phải thứ cần lặp lại ở đây.
                        ["amount"] = command.Amount.ToString(),
                        ["reason"] = refund.Reason ?? string.Empty,
                        ["collected"] = invoice.Collected.ToString()
                    }),
                ct);

            return refund;
        }, cancellationToken);

        return SalesInvoiceMapper.ToDto(invoice);
    }
}
