using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Platform.Tenants;
using NailManagement.Domain.Salon.Branches;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Infrastructure.Persistence;

namespace NailManagement.Infrastructure.Persistence.Platform.Tenants;

/// <summary>
/// Bản cài đặt <see cref="ITenantRepository"/> bằng EF Core.
/// <para>
/// Trả về tiệm <b>không kèm</b> gói đăng ký — tiệm và gói là hai aggregate. Nơi cần cả hai đọc
/// qua <c>TenantPlanReader</c>; bảng gói chỉ có vài dòng nên lượt đọc thêm ấy không đáng kể.
/// </para>
/// <para>
/// Bộ lọc xóa mềm (BR-DEL-002) được <c>NailDbContext</c> gắn sẵn, nên tiệm đã xóa không
/// bao giờ lọt ra khỏi đây — trừ đúng một hàm được ghi chú rõ bên dưới.
/// </para>
/// </summary>
public sealed class TenantRepository(NailDbContext db) : ITenantRepository
{
    public async Task<Tenant?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.Tenants
            .FirstOrDefaultAsync(tenant => tenant.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Tenant>> ListByIdsAsync(
        IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];

        return await db.Tenants
            .Where(tenant => ids.Contains(tenant.Id))
            .OrderBy(tenant => tenant.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> ListAllAsync(CancellationToken cancellationToken = default)
        => await db.Tenants
            // Tiệm mới nhất lên đầu: Superadmin vừa tạo xong một tiệm thì việc đầu tiên họ
            // muốn thấy là chính nó, không phải cuộn xuống cuối danh sách để tìm.
            .OrderByDescending(tenant => tenant.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// BR-VAL-001 — mã tiệm duy nhất toàn hệ thống.
    /// <para>
    /// ⚠️ Cố ý <c>IgnoreQueryFilters</c>: tiệm đã xóa mềm vẫn chiếm mã của nó, vì bản ghi vẫn
    /// nằm trong bảng và chỉ số duy nhất ở tầng database không biết tới chuyện xóa mềm. Bỏ
    /// dòng đó thì phép kiểm ở đây báo "mã còn trống", rồi lệnh ghi ngay sau bị database từ
    /// chối bằng một lỗi ràng buộc thô mà người dùng không hiểu.
    /// </para>
    /// </summary>
    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();

        return await db.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(tenant => tenant.Code == normalized, cancellationToken);
    }

    /// <summary>
    /// Đếm chi nhánh và nhân viên đang hoạt động của từng tiệm.
    /// <para>
    /// ⚠️ Đây là chỗ <b>duy nhất</b> trong hệ thống đọc hai bảng nghiệp vụ mà bỏ qua bộ lọc
    /// theo tiệm, và nó phải làm vậy vì tài khoản Superadmin không thuộc tiệm nào — với họ
    /// bộ lọc luôn đóng và mọi phép đếm sẽ ra 0.
    /// </para>
    /// <para>
    /// Phép cách ly vẫn được giữ theo hai cách. Thứ nhất, chỉ <b>con số</b> rời khỏi hàm này:
    /// không tên khách, không lịch hẹn, không doanh thu — đúng ranh giới BR-AUTH-030 đặt ra.
    /// Thứ hai, danh sách tiệm cần đếm do người gọi truyền vào, nên không có đường nào để
    /// một lời gọi vô tình quét toàn bộ database.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, TenantUsage>> ReadUsageAsync(
        IReadOnlyCollection<string> tenantIds, CancellationToken cancellationToken = default)
    {
        if (tenantIds.Count == 0) return new Dictionary<string, TenantUsage>();

        var branchCounts = await db.Branches
            .IgnoreQueryFilters()
            .Where(branch => tenantIds.Contains(branch.TenantId) && branch.Status == BranchStatus.Active)
            .GroupBy(branch => branch.TenantId)
            .Select(group => new { TenantId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.TenantId, row => row.Count, cancellationToken);

        // BR-EMP-006 — "nghỉ việc" là trạng thái Inactive, và người đã nghỉ không chiếm chỗ
        // trong hạn mức max_staff. Ba trạng thái còn lại đều là người đang trong biên chế.
        var staffCounts = await db.Staff
            .IgnoreQueryFilters()
            .Where(member => tenantIds.Contains(member.TenantId) && member.Status != StaffStatus.Inactive)
            .GroupBy(member => member.TenantId)
            .Select(group => new { TenantId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.TenantId, row => row.Count, cancellationToken);

        return tenantIds.ToDictionary(
            id => id,
            id => new TenantUsage(
                branchCounts.TryGetValue(id, out var branches) ? branches : 0,
                staffCounts.TryGetValue(id, out var staff) ? staff : 0));
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await db.Tenants.AddAsync(tenant, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        db.Tenants.Update(tenant);
        await db.SaveChangesAsync(cancellationToken);
    }
}
