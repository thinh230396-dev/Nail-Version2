using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Auditing;
using NailManagement.Infrastructure.Persistence;

namespace NailManagement.Infrastructure.Persistence.Auditing;

/// <summary>
/// Bản cài đặt <see cref="IAuditLogRepository"/> bằng EF Core.
/// <para>
/// Ghi ngay và ghi riêng, không chờ chung giao dịch với nghiệp vụ. Với sự kiện đăng nhập
/// hỏng thì đó là điều bắt buộc: thao tác nghiệp vụ kết thúc bằng một ngoại lệ, nếu bản ghi
/// nhật ký nằm chung giao dịch thì nó sẽ bị hủy theo — và mất đúng những dòng cần nhất.
/// </para>
/// </summary>
public sealed class AuditLogRepository(NailDbContext db) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
    {
        await db.AuditLogs.AddAsync(entry, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> ListAsync(
        string? tenantId, int take, CancellationToken cancellationToken = default)
    {
        var query = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.Where(entry => entry.TenantId == tenantId);

        return await query
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
