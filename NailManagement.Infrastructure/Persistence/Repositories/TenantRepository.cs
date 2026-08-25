using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Entities;
using NailManagement.Domain.Repositories;

namespace NailManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Bản cài đặt <see cref="ITenantRepository"/> bằng EF Core.
/// <para>
/// Mọi truy vấn ở đây đều nạp kèm gói đăng ký. Lý do: cả hai chỗ dùng tới tiệm — dựng phạm
/// vi phiên và màn chọn tiệm — đều cần biết gói mở những tính năng nào (BR-SUB-007). Để
/// tầng trên tự đi hỏi thêm một lượt nữa là thêm một vòng xuống database ở mỗi request.
/// </para>
/// <para>
/// Bộ lọc xóa mềm (BR-DEL-002) được <c>NailDbContext</c> gắn sẵn, nên tiệm đã xóa không
/// bao giờ lọt ra khỏi đây.
/// </para>
/// </summary>
public sealed class TenantRepository(NailDbContext db) : ITenantRepository
{
    public async Task<Tenant?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.Tenants
            .Include(tenant => tenant.Package)
            .FirstOrDefaultAsync(tenant => tenant.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Tenant>> ListByIdsAsync(
        IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];

        return await db.Tenants
            .Include(tenant => tenant.Package)
            .Where(tenant => ids.Contains(tenant.Id))
            .OrderBy(tenant => tenant.Name)
            .ToListAsync(cancellationToken);
    }
}
