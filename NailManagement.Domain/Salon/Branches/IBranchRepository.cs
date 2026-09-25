using NailManagement.Domain.Shared;

namespace NailManagement.Domain.Salon.Branches;

/// <summary>
/// Cổng ra kho dữ liệu chi nhánh.
/// <para>
/// Không hàm nào ở đây nhận mã tiệm làm tham số, và đó là điều cố ý: chi nhánh mang
/// <c>ITenantOwned</c> nên bộ lọc toàn cục ở <c>NailDbContext</c> đã gắn sẵn điều kiện theo
/// tiệm đang làm việc (BR-ISO-002). Thêm một tham số <c>tenantId</c> vào đây là mời người
/// viết use case tự chọn tiệm — đúng thứ mà bộ lọc dùng chung sinh ra để ngăn.
/// </para>
/// </summary>
public interface IBranchRepository
{
    /// <summary>Toàn bộ chi nhánh của tiệm đang làm việc, kể cả chi nhánh đã ngừng (BR-DEL-003).</summary>
    Task<IReadOnlyList<Branch>> ListAsync(CancellationToken cancellationToken = default);

    Task<Branch?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>BR-BRANCH-005 — đếm chi nhánh đang hoạt động để đối chiếu với <c>max_salons</c>.</summary>
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// BR-VAL-001 — tên chi nhánh duy nhất trong một tiệm. Kiểm ở đây trước khi ghi để người
    /// dùng nhận thông báo gắn đúng ô nhập, thay vì một lỗi ràng buộc thô từ database.
    /// </summary>
    /// <param name="exceptBranchId">Bỏ qua chính chi nhánh đang sửa, nếu không thì nó tự trùng tên với mình.</param>
    Task<bool> NameExistsAsync(
        string name, string? exceptBranchId, CancellationToken cancellationToken = default);

    Task AddAsync(Branch branch, CancellationToken cancellationToken = default);

    Task UpdateAsync(Branch branch, CancellationToken cancellationToken = default);
}
