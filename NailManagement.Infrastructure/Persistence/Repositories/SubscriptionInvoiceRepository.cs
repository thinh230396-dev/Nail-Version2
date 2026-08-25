using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Entities;
using NailManagement.Domain.Repositories;

namespace NailManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Bản cài đặt <see cref="ISubscriptionInvoiceRepository"/> bằng EF Core.
/// <para>
/// Đây là một trong số ít bảng có cột tiệm mà <b>không</b> mang bộ lọc theo tiệm.
/// BR-TENANT-022 là lý do: hóa đơn của tiệm đã xóa mềm vẫn phải đọc được để tính vào doanh
/// thu nền tảng, mà bộ lọc thì sẽ giấu chúng đi.
/// </para>
/// </summary>
public sealed class SubscriptionInvoiceRepository(NailDbContext db) : ISubscriptionInvoiceRepository
{
    public async Task AddAsync(SubscriptionInvoice invoice, CancellationToken cancellationToken = default)
    {
        await db.SubscriptionInvoices.AddAsync(invoice, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await db.SubscriptionInvoices.CountAsync(cancellationToken);
}
