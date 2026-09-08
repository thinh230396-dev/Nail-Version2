using NailManagement.Application.Common;
using NailManagement.Application.DTOs.Salon;
using NailManagement.Application.Mappings.Salon;
using NailManagement.Domain.Repositories.Salon;

namespace NailManagement.Application.UseCases.SalesInvoices;

/// <summary>
/// Một hóa đơn, cho ngăn chi tiết và cho màn thu tiền đọc lại sau mỗi lần ghi.
/// <para>
/// Trả về đúng hình dạng <see cref="SalesInvoiceDto"/> của danh sách, không có bản "chi tiết"
/// giàu hơn: một hóa đơn đã mang sẵn trọn vẹn nội dung của nó, gồm cả các dòng hàng và các dòng
/// thu tiền.
/// </para>
/// </summary>
public sealed class GetSalesInvoiceUseCase(ISalesInvoiceRepository invoices)
{
    public async Task<SalesInvoiceDto> ExecuteAsync(
        string id, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var invoice = BranchScope.EnsureInScope(
            await invoices.FindByIdAsync(id ?? string.Empty, cancellationToken),
            actor,
            "Không tìm thấy hóa đơn.");

        return SalesInvoiceMapper.ToDto(invoice);
    }
}
