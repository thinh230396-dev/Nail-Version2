using NailManagement.Application.Common;
using NailManagement.Domain.Salon.Invoices;

namespace NailManagement.Application.Features.SalesInvoices;

/// <summary>
/// Dựng <see cref="SalesInvoiceDto"/> từ hóa đơn: đọc danh bạ chi nhánh, khách và kỹ thuật
/// viên, rồi ghép lại qua mapper.
/// <para>
/// Bảy use case hóa đơn đều kết thúc bằng bước này. Gom về một chỗ để không use case nào phải
/// tự nhớ đọc đủ ba bảng — cùng lý do <c>TenantReadService</c> tồn tại ở module tiệm.
/// </para>
/// </summary>
public sealed class SalesInvoiceReadService(ISalonDirectoryReader directory)
{
    public async Task<SalesInvoiceDto> DescribeAsync(
        SalesInvoice invoice, CancellationToken cancellationToken = default)
    {
        var names = await directory.ForInvoicesAsync([invoice], cancellationToken);

        return SalesInvoiceMapper.ToDto(invoice, names);
    }

    public async Task<IReadOnlyList<SalesInvoiceDto>> DescribeManyAsync(
        IReadOnlyList<SalesInvoice> invoices, CancellationToken cancellationToken = default)
    {
        if (invoices.Count == 0) return [];

        var names = await directory.ForInvoicesAsync(invoices, cancellationToken);

        return [.. invoices.Select(invoice => SalesInvoiceMapper.ToDto(invoice, names))];
    }
}
