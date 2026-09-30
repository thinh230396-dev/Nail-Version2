using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Salon.Invoices;
using NailManagement.Domain.Salon.Revenue;
using NailManagement.Domain.Shared;
using NailManagement.Infrastructure.Persistence;

namespace NailManagement.Infrastructure.Persistence.Salon.Revenue;

/// <summary>
/// Bản cài đặt <see cref="IRevenueRepository"/> bằng EF Core.
///
/// <para>
/// Không câu truy vấn nào ở đây viết <c>Where(i =&gt; i.TenantId == ...)</c>: điều kiện đó đã
/// được <c>NailDbContext</c> gắn sẵn cho mọi entity mang <c>ITenantOwned</c> (BR-ISO-002), và
/// hóa đơn cùng hai bảng con của nó đều mang giao diện ấy.
/// </para>
/// </summary>
public sealed class RevenueRepository(NailDbContext db) : IRevenueRepository
{
    public async Task<IReadOnlyList<SalesInvoice>> ListCollectedBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? branchId,
        CancellationToken cancellationToken = default)
    {
        // Điều kiện lọc nằm ở DÒNG THU, không ở hóa đơn: BR-REV-001 ghi nhận doanh thu theo
        // tiền thực thu. Một hóa đơn lập tháng trước mà khách trả nốt hôm nay vẫn phải có mặt
        // trong báo cáo hôm nay, và lọc theo giờ lập sẽ đánh rơi đúng nó.
        var query = db.SalesInvoices
            .Where(invoice => invoice.Payments.Any(
                payment => payment.PaidAt >= from && payment.PaidAt < to));

        if (branchId is not null)
            query = query.Where(invoice => invoice.BranchId == branchId);

        return await query
            .Include(invoice => invoice.Lines)
            .Include(invoice => invoice.Payments)

            // Hai phép nối một–nhiều lồng nhau. Gộp vào một câu thì mỗi hóa đơn bị nhân lên
            // bằng số dòng hàng nhân số dòng thu — cùng lý do đã ghi ở kho dữ liệu hóa đơn.
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
