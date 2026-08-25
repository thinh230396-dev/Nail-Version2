using NailManagement.Application.Abstractions;
using NailManagement.Application.DTOs;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Tenants;

/// <summary>
/// Danh sách tiệm cho màn quản lý của Superadmin.
/// <para>
/// Tiệm đã xóa mềm không xuất hiện: bộ lọc <c>DeletedAt == null</c> ở <c>NailDbContext</c>
/// lo việc đó (BR-DEL-002, BR-TENANT-020), nên use case này không có một dòng nào nói về
/// chuyện xóa — và cũng không thể quên nói.
/// </para>
/// </summary>
public sealed class ListTenantsUseCase(
    ITenantRepository tenants,
    TenantReadService reader,
    IClock clock)
{
    public async Task<IReadOnlyList<TenantDetailDto>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await tenants.ListAllAsync(cancellationToken);

        return await reader.DescribeManyAsync(all, clock.UtcNow, cancellationToken);
    }
}
