using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Features.SalesInvoices;
using NailManagement.Domain.Salon.Invoices;

namespace NailManagement.Application.Features.SalesInvoices.UseCases;

/// <summary>
/// Hủy một hóa đơn — BR-INV-015, và ở lát cắt này đó là <b>chuyển trạng thái duy nhất</b> mà
/// người dùng đặt được bằng tay.
/// <para>
/// Ba trạng thái <c>PENDING</c>, <c>PARTIAL</c> và <c>PAID</c> là <b>kết quả</b> của tổng thu
/// (BR-PAY-003), không phải thứ ai đó chọn: nhận chúng từ client là cho phép đánh dấu một hóa
/// đơn chưa thu đồng nào thành đã thanh toán. <c>REFUNDED</c> thì phải đi kèm số tiền và lý do
/// nên nó có đường riêng ở lát cắt thu tiền. <c>SalesInvoiceMapper.ParseSettableStatus</c> là nơi
/// từ chối cả bốn, kèm câu chữ nói rõ vì sao.
/// </para>
/// <para>
/// Cố ý không có động từ <c>DELETE</c>: BR-DEL-001 không cho xóa cứng thứ gì, và một hóa đơn đã
/// mang số thì số ấy phải tra lại được kể cả khi hóa đơn bị hủy — nếu không thì sổ hóa đơn có
/// một lỗ mà người kiểm tra sổ sách sẽ hỏi.
/// </para>
/// </summary>
public sealed class ChangeSalesInvoiceStatusUseCase(
    ISalesInvoiceRepository invoices,
    SalesInvoiceReadService reader,
    IClock clock)
{
    public async Task<SalesInvoiceDto> ExecuteAsync(
        ChangeSalesInvoiceStatusCommand command,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var invoice = BranchScope.EnsureInScope(
            await invoices.FindByIdAsync(command.InvoiceId ?? string.Empty, cancellationToken),
            actor,
            "Không tìm thấy hóa đơn.");

        // Đọc chuỗi trước khi đụng tới bản ghi: người gửi lên "PAID" phải nhận đúng câu giải
        // thích vì sao trạng thái đó không đặt tay được, chứ không phải một lỗi chung chung.
        SalesInvoiceMapper.ParseSettableStatus(command.Status);

        invoice.Cancel(clock.UtcNow);

        await invoices.UpdateAsync(invoice, cancellationToken);

        return await reader.DescribeAsync(invoice, cancellationToken);
    }
}
