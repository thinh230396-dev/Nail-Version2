using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs.Salon;
using NailManagement.Application.Mappings.Salon;
using NailManagement.Domain.Repositories.Salon;

namespace NailManagement.Application.UseCases.SalesInvoices;

/// <summary>
/// Sửa trọn một hóa đơn chưa thu đủ — BR-INV-015.
/// <para>
/// Thay cả danh sách dòng, giảm giá, tip, ghi chú và kỹ thuật viên được ghi công, cùng khuôn
/// thay-trọn đã dùng cho nhân viên, khách hàng và lịch hẹn: bỏ trống ghi chú là xóa ghi chú.
/// </para>
/// <para>
/// BR-INV-014 chặn ở tầng Domain: hóa đơn đã thanh toán, đã hoàn tiền hoặc đã hủy thì
/// <c>EnsureEditable</c> từ chối mọi lệnh sửa. Sai sót trên một hóa đơn đã thu đủ chỉ xử lý bằng
/// hoàn tiền — đó là điều làm cho một hóa đơn đã in trở thành chứng từ đáng tin.
/// </para>
/// <para>
/// Không đổi được khách, lịch hẹn hay chi nhánh: ba thứ đó là danh tính của hóa đơn. Đổi chúng
/// là lập một hóa đơn khác, và khi đó việc đúng là hủy cái cũ rồi lập cái mới — để số hóa đơn
/// cũ vẫn tra được thay vì lặng lẽ mang một nội dung khác.
/// </para>
/// </summary>
public sealed class UpdateSalesInvoiceUseCase(
    ISalesInvoiceRepository invoices,
    IStaffRepository staffMembers,
    SalesInvoiceLineBuilder lineBuilder,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<SalesInvoiceDto> ExecuteAsync(
        UpdateSalesInvoiceCommand command,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var invoice = BranchScope.EnsureInScope(
            await invoices.FindByIdAsync(command.InvoiceId ?? string.Empty, cancellationToken),
            actor,
            "Không tìm thấy hóa đơn.");

        var lines = await lineBuilder.FromInputAsync(command.Lines, cancellationToken);
        var staffId = await ResolveStaffAsync(command.StaffId, actor, cancellationToken);

        // Bỏ hết rồi thêm lại, chứ không so từng dòng để tìm cái nào đổi. Dòng hóa đơn không có
        // danh tính bền vững dưới mắt người dùng — họ xóa một dòng rồi thêm một dòng khác, không
        // ai "sửa dòng số hai". So khớp ở đây chỉ dựng ra một phép đối chiếu phức tạp cho một
        // câu hỏi mà nghiệp vụ không hỏi.
        invoice.ClearLines(now);

        foreach (var line in lines)
            invoice.AddLine(ids.NewId("INL"), line.ServiceId, line.Name, line.UnitPrice, line.Quantity, now);

        // Giảm giá luôn được gán lại, kể cả khi bằng 0: đây là phép thay trọn, nên bỏ trống ô
        // giảm giá trong biểu mẫu phải có nghĩa là gỡ khoản giảm đi.
        invoice.ApplyDiscount(command.Discount, command.DiscountReason, now);
        invoice.SetTip(command.Tip, now);
        invoice.AssignStaff(staffId, now);
        invoice.UpdateNote(command.Note, now);

        await invoices.UpdateAsync(invoice, cancellationToken);

        var saved = await invoices.FindByIdAsync(invoice.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Hóa đơn {invoice.Id} vừa lưu xong nhưng đọc lại không thấy.");

        return SalesInvoiceMapper.ToDto(saved);
    }

    private async Task<string?> ResolveStaffAsync(
        string? staffId, ActorContext actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(staffId)) return null;

        var staff = await staffMembers.FindByIdAsync(staffId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kỹ thuật viên cho hóa đơn này.");

        return BranchScope.EnsureInScope(
            staff, actor, "Không tìm thấy kỹ thuật viên này trong chi nhánh của bạn.").Id;
    }
}
