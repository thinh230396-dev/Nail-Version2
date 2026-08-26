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
}
