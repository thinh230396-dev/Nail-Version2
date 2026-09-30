using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Salon.Services;
using NailManagement.Domain.Shared;
using NailManagement.Infrastructure.Persistence;

namespace NailManagement.Infrastructure.Persistence.Salon.Services;

/// <summary>
/// Bản cài đặt <see cref="IServiceRepository"/> bằng EF Core.
/// <para>
/// Không câu truy vấn nào ở đây viết <c>Where(s =&gt; s.TenantId == ...)</c>: điều kiện đó
/// đã được <c>NailDbContext</c> gắn sẵn cho mọi entity mang <c>ITenantOwned</c> (BR-ISO-002).
/// Viết lại bằng tay chỉ tạo ra chỗ để quên, và quên đúng một lần là lộ bảng giá của tiệm khác.
/// </para>
/// </summary>
public sealed class ServiceRepository(NailDbContext db) : IServiceRepository
{
    public async Task<IReadOnlyList<Service>> ListAsync(CancellationToken cancellationToken = default)
        => await db.Services
            // Dịch vụ đang bán lên trước, phần đã ngừng dồn xuống cuối, trong mỗi nhóm sắp
            // theo tên. Sắp ở đây một lần thì mọi màn hình khỏi tự sắp lại theo cách khác.
            .OrderBy(service => service.Status == ServiceStatus.Inactive)
            .ThenBy(service => service.Name)
            .ToListAsync(cancellationToken);

    public async Task<Service?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.Services.FirstOrDefaultAsync(service => service.Id == id, cancellationToken);

    public async Task<bool> NameExistsAsync(
        string name, string? exceptServiceId, CancellationToken cancellationToken = default)
    {
        var normalized = (name ?? string.Empty).Trim();

        return await db.Services.AnyAsync(
            service => service.Name == normalized
                       && (exceptServiceId == null || service.Id != exceptServiceId),
            cancellationToken);
    }

    public async Task AddAsync(Service service, CancellationToken cancellationToken = default)
    {
        await db.Services.AddAsync(service, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Service service, CancellationToken cancellationToken = default)
    {
        db.Services.Update(service);
        await db.SaveChangesAsync(cancellationToken);
    }
}
