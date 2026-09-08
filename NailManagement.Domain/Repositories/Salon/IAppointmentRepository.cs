using NailManagement.Domain.Entities.Salon;

namespace NailManagement.Domain.Repositories.Salon;

/// <summary>
/// Cổng ra kho dữ liệu lịch hẹn.
/// <para>
/// Không hàm nào nhận mã tiệm, cùng lý do đã ghi ở <see cref="IBranchRepository"/>: lịch hẹn
/// và các dòng dịch vụ của nó đều mang <c>ITenantOwned</c> nên bộ lọc toàn cục ở
/// <c>NailDbContext</c> đã gắn sẵn điều kiện theo tiệm đang làm việc (BR-ISO-002).
/// </para>
/// <para>
/// Ngược lại, hai hàm ở đây <b>có</b> nhận mã chi nhánh — khác hẳn <see cref="ICustomerRepository"/>.
/// BR-APT-002 quy định lễ tân chỉ thao tác lịch hẹn thuộc chi nhánh mình, nên chi nhánh ở
/// đây là một phép thu hẹp thật theo ma trận quyền, không phải một cách chia dữ liệu tự nghĩ ra.
/// </para>
/// </summary>
public interface IAppointmentRepository
{
    /// <summary>
    /// Lịch hẹn có giờ bắt đầu nằm trong khoảng <paramref name="from"/> đến
    /// <paramref name="to"/>, kèm sẵn các dòng dịch vụ, khách và kỹ thuật viên.
    /// <para>
    /// Có khoảng ngày chứ không trả toàn bộ như bốn module trước: danh bạ khách của một tiệm
    /// có trần tự nhiên, còn lịch hẹn thì cộng dồn mãi theo thời gian, trong khi màn hình chỉ
    /// bao giờ hiện một ngày hoặc một tuần.
    /// </para>
    /// </summary>
    /// <param name="branchId">
    /// Giới hạn theo một chi nhánh, hoặc <c>null</c> để lấy cả tiệm — BR-APT-002. Giá trị
    /// luôn đến từ phiên đăng nhập qua <c>ActorContext.BranchId</c>, không bao giờ từ thân
    /// request hay chuỗi truy vấn.
    /// </param>
    Task<IReadOnlyList<Appointment>> ListAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? branchId,
        CancellationToken cancellationToken = default);

    /// <summary>Một lịch hẹn kèm các dòng dịch vụ của nó, hoặc rỗng nếu không thuộc tiệm đang làm việc.</summary>
    Task<Appointment?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// BR-APT-011 — lịch hẹn đầu tiên đang chặn khoảng giờ này của kỹ thuật viên này, hoặc
    /// rỗng nếu khoảng giờ còn trống.
    /// <para>
    /// Trả về <b>chính lịch hẹn đang chặn</b> chứ không phải một giá trị đúng/sai, vì thông
    /// báo lỗi phải nói được ai đang giữ chỗ và giữ từ mấy giờ tới mấy giờ. Một câu "kỹ thuật
    /// viên đã bận" không giúp lễ tân biết nên xếp khách vào lúc nào.
    /// </para>
    /// <para>
    /// Điều kiện chồng lấn không được viết ở đây: nó đến từ
    /// <c>AppointmentSchedulePolicy.BlockingSlot</c> ở tầng Domain, để luật chống trùng chỉ
    /// có đúng một định nghĩa trong toàn hệ thống.
    /// </para>
    /// </summary>
    /// <param name="exceptAppointmentId">Chính lịch hẹn đang sửa hoặc đang dời — nếu không nó sẽ tự trùng giờ với mình.</param>
    Task<Appointment?> FindBlockingAsync(
        string staffId,
        DateTimeOffset start,
        DateTimeOffset end,
        string? exceptAppointmentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);

    Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default);
}
