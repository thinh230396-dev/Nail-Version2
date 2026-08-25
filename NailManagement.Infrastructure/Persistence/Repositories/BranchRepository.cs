using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Entities;
using NailManagement.Domain.Enums;
using NailManagement.Domain.Repositories;

namespace NailManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Bản cài đặt <see cref="IBranchRepository"/> bằng EF Core.
/// <para>
/// Không câu truy vấn nào ở đây viết <c>Where(b =&gt; b.TenantId == ...)</c>, và đó là điểm
/// đáng chú ý nhất của lớp này: điều kiện đó đã được <c>NailDbContext</c> gắn sẵn cho mọi
/// entity mang <c>ITenantOwned</c> (BR-ISO-002). Viết lại bằng tay chỉ tạo ra chỗ để quên,
/// và quên đúng một lần là lộ dữ liệu chéo tiệm.
/// </para>
/// </summary>
public sealed class BranchRepository(NailDbContext db) : IBranchRepository
{
    public async Task<IReadOnlyList<Branch>> ListAsync(CancellationToken cancellationToken = default)
        => await db.Branches
            // Chi nhánh chính lên đầu, phần còn lại theo tên: đó là thứ tự mà màn hình quản
            // lý chi nhánh đang hiển thị, nên sắp ở đây thì mọi màn hình khỏi tự sắp lại.
            .OrderByDescending(branch => branch.IsPrimary)
            .ThenBy(branch => branch.Name)
            .ToListAsync(cancellationToken);

    public async Task<Branch?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.Branches.FirstOrDefaultAsync(branch => branch.Id == id, cancellationToken);

    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
        => await db.Branches.CountAsync(branch => branch.Status == BranchStatus.Active, cancellationToken);

    public async Task<bool> NameExistsAsync(
        string name, string? exceptBranchId, CancellationToken cancellationToken = default)
    {
        var normalized = (name ?? string.Empty).Trim();

        return await db.Branches.AnyAsync(
            branch => branch.Name == normalized && (exceptBranchId == null || branch.Id != exceptBranchId),
            cancellationToken);
    }

    public async Task AddAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        await db.Branches.AddAsync(branch, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        db.Branches.Update(branch);
        await db.SaveChangesAsync(cancellationToken);
    }
}
