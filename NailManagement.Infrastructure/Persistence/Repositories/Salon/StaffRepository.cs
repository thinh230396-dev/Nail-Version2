using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Salon.StaffMembers;

namespace NailManagement.Infrastructure.Persistence.Repositories.Salon;

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

    public async Task<IReadOnlyList<Staff>> ListAsync(
        string? branchId, CancellationToken cancellationToken = default)
    {
        // Đây là hàm DUY NHẤT trong kho dữ liệu này còn nhận một tham số phạm vi. Nó hợp lệ
        // vì chi nhánh không phải ranh giới cách ly — bộ lọc theo tiệm đã làm việc đó rồi —
        // mà chỉ là một phép thu hẹp theo ma trận quyền cho vai trò lễ tân.
        var query = branchId is null
            ? db.Staff
            : db.Staff.Where(staff => staff.BranchId == branchId);

        return await query
            // Người đang làm lên trước, người đã nghỉ việc dồn xuống cuối, trong mỗi nhóm
            // sắp theo tên.
            .OrderBy(staff => staff.Status == StaffStatus.Inactive)
            .ThenBy(staff => staff.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<Staff?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.Staff.FirstOrDefaultAsync(staff => staff.Id == id, cancellationToken);

    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
        => await db.Staff.CountAsync(staff => staff.Status != StaffStatus.Inactive, cancellationToken);

    public async Task AddAsync(Staff staff, CancellationToken cancellationToken = default)
    {
        await db.Staff.AddAsync(staff, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Staff staff, CancellationToken cancellationToken = default)
    {
        db.Staff.Update(staff);
        await db.SaveChangesAsync(cancellationToken);
    }
}
