using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Platform.Packages;
using NailManagement.Domain.Shared;
using NailManagement.Infrastructure.Persistence;

namespace NailManagement.Infrastructure.Persistence.Platform.Packages;

/// <summary>
/// Bản cài đặt <see cref="IPackageRepository"/> bằng EF Core.
/// <para>
/// Bảng gói thuộc tầng nền tảng, không mang <c>ITenantOwned</c>, nên không có bộ lọc theo
/// tiệm nào chạy ở đây. Đúng như vậy: bảng giá là chung cho mọi tiệm.
/// </para>
/// </summary>
public sealed class PackageRepository(NailDbContext db) : IPackageRepository
{
    public async Task<Package?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.Packages.FirstOrDefaultAsync(package => package.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Package>> ListAsync(CancellationToken cancellationToken = default)
        => await db.Packages
            .AsNoTracking()
            .OrderBy(package => package.Price)
            .ToListAsync(cancellationToken);
}
