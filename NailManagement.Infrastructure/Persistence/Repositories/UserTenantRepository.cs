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

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<AppUser>>> ListOwnersAsync(
        IReadOnlyCollection<string> tenantIds, CancellationToken cancellationToken = default)
    {
        if (tenantIds.Count == 0) return new Dictionary<string, IReadOnlyList<AppUser>>();

        var rows = await db.UserTenants
            .Where(link => tenantIds.Contains(link.TenantId))
            .Include(link => link.User)
            // Người được giao sớm nhất đứng đầu, nên "chủ tiệm chính" mà màn hình hiển thị
            // luôn là cùng một người ở mọi lần tải, không đổi theo thứ tự database trả về.
            .OrderBy(link => link.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows
            .Where(link => link.User is not null)
            .GroupBy(link => link.TenantId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AppUser>)[.. group.Select(link => link.User!)]);
    }

    public async Task LinkAsync(
        string userId, string tenantId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await db.UserTenants.AddAsync(UserTenant.Link(userId, tenantId, now), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// BR-TENANT-021 — gỡ mọi liên kết của một tiệm.
    /// <para>
    /// Đây là chỗ duy nhất trong hệ thống thật sự xóa dòng khỏi database, và nó không mâu
    /// thuẫn với BR-DEL-001: bảng này không lưu dữ liệu nghiệp vụ nào, nó chỉ ghi lại "ai
    /// đang được giao tiệm nào". Gỡ một quyền thì đúng là quyền đó biến mất — giữ lại một
    /// dòng "đã từng được giao" chỉ tạo ra nguy cơ có ngày nó được đọc như quyền thật.
    /// </para>
    /// </summary>
    public async Task UnlinkAllAsync(string tenantId, CancellationToken cancellationToken = default)
        => await db.UserTenants
            .Where(link => link.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);
}
