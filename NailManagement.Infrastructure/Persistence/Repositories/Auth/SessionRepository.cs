using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Repositories.Auth;

namespace NailManagement.Infrastructure.Persistence.Repositories.Auth;

public sealed class SessionRepository(NailDbContext db) : ISessionRepository
{
    public async Task AddAsync(AppSession session, CancellationToken cancellationToken = default)
    {
        await db.AppSessions.AddAsync(session, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AppSession?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.AppSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    /// <summary>
    /// Lưu những gì đã đổi trên bản ghi phiên đang được theo dõi.
    /// <para>
    /// Cố ý KHÔNG gọi <c>db.AppSessions.Update(...)</c>, cùng lý do với kho dữ liệu lịch hẹn và
    /// hóa đơn: hàm đó đánh dấu <b>mọi</b> cột là đã sửa, kể cả những cột mà request này không
    /// hề chạm tới. Phiên là bản ghi bị ghi nhiều nhất của cả hệ thống — mỗi request đều
    /// <c>Touch</c> một lần — nên nó cũng là chỗ dễ ghi đè nhau nhất.
    /// </para>
    /// <para>
    /// Kịch bản thật đã bị chặn bởi dòng này: request A đọc phiên rồi cập nhật <c>LastActive</c>;
    /// trong lúc ấy request B thu hồi chính phiên đó. Với <c>Update</c>, câu lệnh của A ghi lại
    /// <b>cả</b> <c>RevokedAt = NULL</c> theo bản chụp cũ nó đọc được, và phiên vừa bị thu hồi
    /// sống lại. Chỉ lưu phần đã đổi thì câu lệnh chỉ đụng tới đúng <c>LastActive</c>.
    /// </para>
    /// </summary>
    public async Task UpdateAsync(AppSession session, CancellationToken cancellationToken = default)
        => await db.SaveChangesAsync(cancellationToken);

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
