using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Repositories;

namespace NailManagement.Infrastructure.Persistence.Repositories;

public sealed class SessionRepository(NailDbContext db) : ISessionRepository
{
    public async Task AddAsync(AppSession session, CancellationToken cancellationToken = default)
    {
        await db.AppSessions.AddAsync(session, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AppSession?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.AppSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task UpdateAsync(AppSession session, CancellationToken cancellationToken = default)
    {
        db.AppSessions.Update(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AppSession>> ListAsync(
        string? tenantId, int take, CancellationToken cancellationToken = default)
    {
        // Include chủ phiên vì DTO cần tên, email và vai trò. Không Include thì mỗi dòng là
        // một lượt truy vấn thêm, và danh sách hai chục phiên hóa ra hai chục lượt đi về.
        var query = db.AppSessions.AsNoTracking().Include(session => session.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var members = db.UserTenants
                .AsNoTracking()
                .Where(link => link.TenantId == tenantId)
                .Select(link => link.UserId);

            query = query.Where(session => members.Contains(session.UserId));
        }

        return await query
            // Phiên còn hiệu lực lên trước, rồi tới lần hoạt động gần nhất. Người vào màn này
            // đang đi tìm "ai đang mở máy lúc này", không phải đọc lịch sử.
            .OrderBy(session => session.RevokedAt != null)
            .ThenByDescending(session => session.LastActive)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UserBelongsToTenantAsync(
        string userId, string tenantId, CancellationToken cancellationToken = default)
        => await db.UserTenants
            .AsNoTracking()
            .AnyAsync(link => link.UserId == userId && link.TenantId == tenantId, cancellationToken);
}
