using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Entities;
using NailManagement.Domain.Repositories;

namespace NailManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Bản cài đặt <see cref="IUserTenantRepository"/> bằng EF Core.
/// <para>
/// Bảng <c>UserTenants</c> cố ý KHÔNG mang bộ lọc theo tiệm. Nó là thứ được hỏi <b>trước
/// khi</b> phạm vi tiệm được thiết lập — lọc nó theo tiệm đang làm việc sẽ tạo ra vòng lặp
/// tự tham chiếu: muốn biết được vào tiệm nào thì phải đã ở trong một tiệm.
/// </para>
/// </summary>
public sealed class UserTenantRepository(NailDbContext db) : IUserTenantRepository
{
    public async Task<bool> HasAccessAsync(
        string userId, string tenantId, CancellationToken cancellationToken = default)
        => await db.UserTenants
            .AnyAsync(link => link.UserId == userId && link.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<string>> ListTenantIdsAsync(
        string userId, CancellationToken cancellationToken = default)
        => await db.UserTenants
            .Where(link => link.UserId == userId)
            .Select(link => link.TenantId)
            .ToListAsync(cancellationToken);

    public async Task LinkAsync(
        string userId, string tenantId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await db.UserTenants.AddAsync(UserTenant.Link(userId, tenantId, now), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
