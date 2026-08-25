using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Entities;
using NailManagement.Domain.Repositories;

namespace NailManagement.Infrastructure.Persistence.Repositories;

/// <summary>Bản cài đặt <see cref="IStaffRepository"/> bằng EF Core.</summary>
public sealed class StaffRepository(NailDbContext db) : IStaffRepository
{
    public async Task<Staff?> FindForSessionAsync(
        string staffId, CancellationToken cancellationToken = default)
        => await db.Staff
            // Cố ý bỏ qua bộ lọc theo tiệm: lời gọi này diễn ra trong lúc phiên còn đang
            // được dựng, nên chưa có tiệm nào để mà lọc. Use case gọi tới phải tự đối chiếu
            // TenantId của hồ sơ với tiệm đang làm việc — xem chú thích ở IStaffRepository.
            .IgnoreQueryFilters()
            .Include(staff => staff.Branch)
            .FirstOrDefaultAsync(staff => staff.Id == staffId, cancellationToken);
}
