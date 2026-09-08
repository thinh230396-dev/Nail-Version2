using NailManagement.Domain.Entities.Salon;

namespace NailManagement.Domain.Repositories.Salon;

/// <summary>
/// Cổng ra kho dữ liệu dịch vụ.
/// <para>
/// Không hàm nào nhận mã tiệm, cùng lý do đã ghi ở <see cref="IBranchRepository"/>: dịch vụ
/// mang <c>ITenantOwned</c> nên bộ lọc toàn cục ở <c>NailDbContext</c> đã gắn sẵn điều kiện
/// theo tiệm đang làm việc (BR-ISO-002). Thêm một tham số <c>tenantId</c> vào đây là mời
/// người viết use case tự chọn tiệm.
/// </para>
/// </summary>
public interface IServiceRepository
{
    /// <summary>
    /// Toàn bộ dịch vụ của tiệm đang làm việc, <b>kể cả dịch vụ đã ngừng bán</b>.
    /// <para>
    /// BR-DEL-003 — bản ghi <c>INACTIVE</c> vẫn phải hiện đúng tên trong hóa đơn cũ, và màn
    /// quản lý cần nhìn thấy chúng thì mới có đường bật lại. Việc loại dịch vụ đã ngừng ra
    /// khỏi ô chọn khi lập lịch hẹn là quyết định của màn hình đó, không phải của kho dữ liệu.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Service>> ListAsync(CancellationToken cancellationToken = default);

    Task<Service?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// BR-VAL-001 — tên dịch vụ duy nhất trong một tiệm. Kiểm ở đây trước khi ghi để người
    /// dùng nhận thông báo gắn đúng ô nhập, thay vì một lỗi ràng buộc thô từ database.
    /// </summary>
    /// <param name="exceptServiceId">Bỏ qua chính dịch vụ đang sửa, nếu không thì nó tự trùng tên với mình.</param>
    Task<bool> NameExistsAsync(
        string name, string? exceptServiceId, CancellationToken cancellationToken = default);

    Task AddAsync(Service service, CancellationToken cancellationToken = default);

    Task UpdateAsync(Service service, CancellationToken cancellationToken = default);
}
