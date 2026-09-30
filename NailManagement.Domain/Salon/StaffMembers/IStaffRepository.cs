namespace NailManagement.Domain.Salon.StaffMembers;

/// <summary>
/// Chi nhánh của một nhân viên, đọc gọn cho phiên đăng nhập — chỉ những gì màn hình cần để
/// hiện "đang làm ở đâu", không phải cả hai aggregate.
/// </summary>
/// <param name="TenantId">Tiệm của hồ sơ nhân viên — người gọi phải đối chiếu với tiệm của phiên.</param>
public sealed record StaffBranchScope(
    string StaffId,
    string TenantId,
    string BranchId,
    string? BranchCode,
    string BranchName);

/// <summary>Cổng ra kho dữ liệu nhân viên.</summary>
public interface IStaffRepository
{
    /// <summary>
    /// Đọc chi nhánh mà một nhân viên thuộc về, phục vụ việc dựng phạm vi cho phiên đăng nhập.
    /// <para>
    /// ⚠️ Hàm này chạy <b>trước khi</b> phạm vi tiệm của request được thiết lập — nó chính
    /// là một phần của việc thiết lập đó — nên nó phải bỏ qua bộ lọc theo tiệm, thứ mà mọi
    /// truy vấn khác đều đi qua.
    /// </para>
    /// <para>
    /// Vì vậy người gọi <b>bắt buộc</b> phải tự đối chiếu <c>TenantId</c> của hồ sơ trả về
    /// với tiệm đang làm việc của phiên. Đây là chỗ duy nhất trong hệ thống mà phép cách ly
    /// tenant không tự động, nên nó được viết rõ ở cả hai đầu: ở đây và ở use case gọi tới.
    /// </para>
    /// </summary>
    Task<StaffBranchScope?> FindBranchScopeForSessionAsync(
        string staffId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hồ sơ nhân viên của tiệm đang làm việc, kể cả người đã nghỉ việc (BR-DEL-003).
    /// </summary>
    /// <param name="branchId">
    /// Giới hạn theo một chi nhánh, hoặc <c>null</c> để lấy cả tiệm. Đây là cách ma trận
    /// mục 3.4 được cưỡng chế cho lễ tân — họ chỉ xem nhân viên <i>chi nhánh mình</i> — và
    /// giá trị luôn đến từ phiên đăng nhập qua <c>ActorContext.BranchId</c>, không bao giờ
    /// từ thân request.
    /// </param>
    Task<IReadOnlyList<Staff>> ListAsync(
        string? branchId, CancellationToken cancellationToken = default);

    Task<Staff?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhiều hồ sơ nhân viên theo mã, trong tiệm đang làm việc, bằng MỘT truy vấn — cho các màn danh sách
    /// cần hiện tên thay vì mã. Mã không tìm thấy thì vắng mặt trong kết quả; bản đọc không
    /// được theo dõi thay đổi.
    /// </summary>
    Task<IReadOnlyDictionary<string, Staff>> ListByIdsAsync(
        IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// BR-EMP-008 — đếm nhân viên <b>chưa nghỉ việc</b> để đối chiếu với <c>max_staff</c>.
    /// Người đã nghỉ việc không giữ chỗ hạn mức, giống hệt cách chi nhánh đã ngừng hoạt động
    /// không giữ chỗ <c>max_salons</c>.
    /// </summary>
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Staff staff, CancellationToken cancellationToken = default);

    Task UpdateAsync(Staff staff, CancellationToken cancellationToken = default);
}
