using NailManagement.Domain.Shared;

namespace NailManagement.Domain.Platform.Tenants;

/// <summary>
/// Mức sử dụng thật của một tiệm, đếm từ database.
/// <para>
/// Đây là hai con số DUY NHẤT mà Superadmin được biết về bên trong một tiệm, và chúng có
/// mặt vì một lý do hẹp: BR-BRANCH-005 và BR-EMP-008 là hai hạn mức được cưỡng chế thật
/// (BR-SUB-005), nên người bán gói phải nhìn thấy tiệm nào đang chạm trần. Doanh thu, khách
/// hàng, lịch hẹn thì không — BR-AUTH-030 cấm, và bộ lọc theo tiệm ở tầng dữ liệu chặn tiếp
/// một lần nữa.
/// </para>
/// </summary>
public sealed record TenantUsage(int ActiveBranches, int ActiveStaff);

/// <summary>
/// Cổng ra kho dữ liệu tiệm.
/// <para>
/// Trả về tiệm <b>không kèm</b> gói đăng ký: tiệm và gói là hai aggregate, chỉ nối với nhau
/// qua <c>PackageId</c>. Nơi nào cần cả hai — xác định tiệm còn hạn (BR-TENANT-010) rồi gói có
/// mở tính năng không (BR-SUB-007) — đọc qua <c>TenantPlanReader</c> ở tầng Application.
/// </para>
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Đọc nhiều tiệm cùng lúc cho màn chọn tiệm, giữ nguyên thứ tự theo tên.</summary>
    Task<IReadOnlyList<Tenant>> ListByIdsAsync(
        IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toàn bộ tiệm chưa xóa mềm, dành cho màn quản lý tiệm của Superadmin.
    /// <para>
    /// Đây là hàm duy nhất đọc xuyên tiệm, và nó an toàn vì bảng <c>Tenants</c> thuộc tầng
    /// nền tảng chứ không mang <c>ITenantOwned</c> — nó là danh sách khách hàng doanh nghiệp
    /// của SalonSys, không phải dữ liệu bên trong một tiệm.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Tenant>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>BR-VAL-001 — mã tiệm duy nhất toàn hệ thống, kể cả so với tiệm đã xóa mềm.</summary>
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đếm chi nhánh và nhân viên đang hoạt động của từng tiệm.
    /// <para>
    /// ⚠️ Bản cài đặt buộc phải bỏ qua bộ lọc theo tiệm, vì Superadmin không thuộc tiệm nào.
    /// Đó là lý do hàm này chỉ trả về <b>con số</b> chứ không trả về bản ghi: đếm thì không
    /// làm lộ tên khách hàng hay lịch hẹn của tiệm, mà vẫn đủ để cưỡng chế hạn mức.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, TenantUsage>> ReadUsageAsync(
        IReadOnlyCollection<string> tenantIds, CancellationToken cancellationToken = default);

    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);

    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
