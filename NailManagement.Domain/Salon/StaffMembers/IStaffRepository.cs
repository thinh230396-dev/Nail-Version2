namespace NailManagement.Domain.Salon.StaffMembers;

/// <summary>Cổng ra kho dữ liệu nhân viên.</summary>
public interface IStaffRepository
{
    /// <summary>
    /// Đọc hồ sơ nhân viên kèm chi nhánh, phục vụ việc dựng phạm vi cho phiên đăng nhập.
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
    Task<Staff?> FindForSessionAsync(string staffId, CancellationToken cancellationToken = default);

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
    /// BR-EMP-008 — đếm nhân viên <b>chưa nghỉ việc</b> để đối chiếu với <c>max_staff</c>.
    /// Người đã nghỉ việc không giữ chỗ hạn mức, giống hệt cách chi nhánh đã ngừng hoạt động
    /// không giữ chỗ <c>max_salons</c>.
    /// </summary>
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Staff staff, CancellationToken cancellationToken = default);

    Task UpdateAsync(Staff staff, CancellationToken cancellationToken = default);
}
