using NailManagement.Domain.Salon.Branches;
using NailManagement.Domain.Salon.Customers;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.Shared;

namespace NailManagement.Domain.Salon.Appointments;

/// <summary>
/// Lịch hẹn của khách — trung tâm của nghiệp vụ tiệm nail.
/// <para>
/// Giờ kết thúc được LƯU thành cột chứ không tính lại mỗi lần đọc. Lý do là phép chống
/// trùng lịch ở BR-APT-011 phải chạy được bằng một câu truy vấn so sánh hai mốc thời gian;
/// nếu giờ kết thúc chỉ tồn tại trong bộ nhớ thì máy chủ buộc phải nạp toàn bộ lịch hẹn
/// của kỹ thuật viên lên rồi mới so được.
/// </para>
/// </summary>
public class Appointment : ITenantOwned, IBranchOwned
{
    private readonly List<AppointmentService> _services = [];

    /// <summary>Dành riêng cho EF Core.</summary>
    private Appointment()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        BranchId = string.Empty;
        CustomerId = string.Empty;
        StaffId = string.Empty;
    }

    private Appointment(
        string id,
        string tenantId,
        string branchId,
        string customerId,
        string staffId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        AppointmentStatus status,
        AppointmentSource source,
        string? station,
        string? note,
        long deposit,
        string? createdByUserId,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        CustomerId = customerId;
        StaffId = staffId;
        StartAt = startAt;
        EndAt = endAt;
        Status = status;
        Source = source;
        Station = station;
        Note = note;
        Deposit = deposit;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Id { get; private set; }
    public string TenantId { get; private set; }

    /// <summary>BR-BRANCH-007 — lịch hẹn thuộc riêng một chi nhánh; lễ tân chỉ thấy chi nhánh mình.</summary>
    public string BranchId { get; private set; }

    public Branch? Branch { get; private set; }

    /// <summary>BR-CUS-004 — mọi lịch hẹn bắt buộc gắn một hồ sơ khách, không có khách vãng lai ẩn danh.</summary>
    public string CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    /// <summary>BR-APT-004 — đúng một kỹ thuật viên phụ trách toàn bộ lịch hẹn.</summary>
    public string StaffId { get; private set; }

    public Staff? Staff { get; private set; }

    public DateTimeOffset StartAt { get; private set; }

    /// <summary>BR-APT-010 — bằng giờ bắt đầu cộng tổng thời lượng và thời gian dọn dẹp.</summary>
    public DateTimeOffset EndAt { get; private set; }

    public AppointmentStatus Status { get; private set; }

    /// <summary>BR-APT-007 — do lễ tân chọn tay, hệ thống không tự sinh.</summary>
    public AppointmentSource Source { get; private set; }

    /// <summary>BR-APT-006 — ghế hoặc phòng, là ô chữ tự do, không tham chiếu bảng nào.</summary>
    public string? Station { get; private set; }

    public string? Note { get; private set; }

    /// <summary>
    /// BR-APT-030 — tiền khách trả trước. BR-APT-032: không có luật hoàn cọc và không có
    /// luật mất cọc; hủy lịch có cọc thì hệ thống chỉ ghi chú, việc thỏa thuận với khách
    /// nằm ngoài phần mềm.
    /// </summary>
    public long Deposit { get; private set; }

    /// <summary>
    /// BR-APT-027 — đánh dấu lịch được chủ tiệm hoàn tất trong khi hóa đơn còn thiếu tiền.
    /// Giữ lại vì đây là ngoại lệ có rủi ro thất thu, và người duyệt đồ án sẽ hỏi làm sao
    /// biết một lịch hoàn tất bình thường với một lịch được đóng tay.
    /// </summary>
    public bool CompletedWithUnpaidBalance { get; private set; }

    public string? CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<AppointmentService> Services => _services;

    /// <summary>
    /// Tạo lịch hẹn kèm các dòng dịch vụ. Danh sách dịch vụ truyền vào là bản chụp lấy từ
    /// bảng dịch vụ ở tầng use case, vì entity không được phép tự đi tra bảng khác.
    /// </summary>
    /// <param name="status">
    /// BR-APT-021 — mặc định là Pending, nhưng lễ tân tạo trực tiếp tại quầy được phép tạo
    /// thẳng ở Confirmed.
    /// </param>
    public static Appointment Create(
        string id,
        string tenantId,
        string branchId,
        string customerId,
        string staffId,
        DateTimeOffset startAt,
        IReadOnlyList<(string ServiceId, string ServiceName, int DurationMinutes, int BufferMinutes)> services,
        AppointmentStatus status,
        AppointmentSource source,
        string? station,
        string? note,
        long deposit,
        string? createdByUserId,
        DateTimeOffset now,
        Func<string>? lineIdFactory = null)
    {
        // BR-VAL-001 — lịch hẹn bắt buộc có ít nhất một dịch vụ. Không có dòng nào thì
        // khoảng thời gian chiếm chỗ bằng 0, và lịch hẹn trở thành một điểm vô nghĩa
        // trên bảng giờ.
        if (services.Count == 0)
            throw DomainException.ForField("services", "Lịch hẹn phải có ít nhất một dịch vụ.");

        if (status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            throw DomainException.ForField("status", "Lịch hẹn mới chỉ được tạo ở trạng thái chờ hoặc đã xác nhận.");

        var totalMinutes = AppointmentSchedulePolicy.TotalMinutes(
            services.Select(service => (service.DurationMinutes, service.BufferMinutes)));

        var appointment = new Appointment(
            Guard.Reference(id, "id", "Mã lịch hẹn"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            Guard.Reference(branchId, "branchId", "Chi nhánh"),
            Guard.Reference(customerId, "customerId", "Khách hàng"),
            Guard.Reference(staffId, "staffId", "Kỹ thuật viên"),
            startAt,
            startAt.AddMinutes(totalMinutes),
            status,
            source,
            Guard.Optional(station, "station", "Ghế hoặc phòng", 80),
            Guard.Optional(note, "note", "Ghi chú lịch hẹn", ValidationPolicy.LongTextMaxLength),
            Guard.Money(deposit, "deposit", "Tiền cọc"),
            createdByUserId,
            now);

        var index = 0;
        appointment.FillServices(services, () => lineIdFactory?.Invoke() ?? $"{appointment.Id}-S{++index}");

        return appointment;
    }

    /// <summary>BR-APT-012 — lịch đã hủy hoặc khách không đến thì không chiếm chỗ của ai.</summary>
    public bool OccupiesSlot() => AppointmentSchedulePolicy.OccupiesSlot(Status);

    public int TotalMinutes() => (int)(EndAt - StartAt).TotalMinutes;

    /// <summary>
    /// BR-APT-025 — dời lịch là sửa giờ bắt đầu trên chính lịch hẹn đó, giữ nguyên trạng
    /// thái và giữ nguyên độ dài. Chỉ cho dời khi còn ở Pending hoặc Confirmed, và tầng
    /// gọi phải chạy lại phép chống trùng ở BR-APT-011 sau khi dời.
    /// </summary>
    public void Reschedule(DateTimeOffset startAt, DateTimeOffset now)
    {
        if (!AppointmentLifecyclePolicy.CanReschedule(Status))
            throw DomainException.ForField("startAt", "Chỉ dời được lịch hẹn đang chờ hoặc đã xác nhận.");

        var minutes = TotalMinutes();
        StartAt = startAt;
        EndAt = startAt.AddMinutes(minutes);
        UpdatedAt = now;
    }

    /// <summary>
    /// Thay trọn nội dung lịch hẹn: chi nhánh, khách, kỹ thuật viên, giờ bắt đầu, danh sách
    /// dịch vụ, nguồn, ghế, ghi chú và tiền cọc. Giờ kết thúc được tính lại từ danh sách
    /// dịch vụ mới theo BR-APT-010, nên tầng gọi <b>phải chạy lại phép chống trùng ở
    /// BR-APT-011</b> sau khi gọi hàm này, đúng như với <see cref="Reschedule"/>.
    /// <para>
    /// Là phép thay trọn chứ không phải vá từng trường, cùng khuôn với <c>PUT /api/staff/{id}</c>
    /// và <c>PUT /api/customers/{id}</c>: bỏ trống ghi chú trong thân request là xóa ghi chú.
    /// Vá từng trường sẽ cần một quy ước phân biệt "không gửi" với "gửi rỗng", thứ mà JSON
    /// không có sẵn và mỗi màn hình sẽ tự hiểu một kiểu.
    /// </para>
    /// <para>
    /// ⚠️ Chặn ở CẢ BA trạng thái cuối, không riêng <c>Completed</c>. BR-APT-023 chỉ nói tới
    /// lịch đã hoàn tất, nhưng sửa một lịch đã hủy hoặc đã ghi khách không đến là dựng lại
    /// một lịch hẹn ở cửa sau: nó vẫn mang trạng thái cũ nên không chiếm chỗ của ai, mà nội
    /// dung thì đã thành một buổi hẹn khác hẳn, và BR-APT-041 nói ba trạng thái ấy không
    /// quay lại được. Muốn đặt lại thì tạo lịch mới.
    /// </para>
    /// </summary>
    /// <param name="lineIdFactory">
    /// Bắt buộc, khác <see cref="Create"/> — các dòng dịch vụ cũ bị bỏ đi và dòng mới phải
    /// mang mã khác. Sinh lại theo đúng công thức của lúc tạo là dựng ra chính những mã vừa
    /// bị xóa trong cùng một lần lưu, và thứ tự xóa rồi thêm khi đó không còn chắc chắn.
    /// </param>
    public void Revise(
        string branchId,
        string customerId,
        string staffId,
        DateTimeOffset startAt,
        IReadOnlyList<(string ServiceId, string ServiceName, int DurationMinutes, int BufferMinutes)> services,
        AppointmentSource source,
        string? station,
        string? note,
        long deposit,
        DateTimeOffset now,
        Func<string> lineIdFactory)
    {
        if (AppointmentLifecyclePolicy.IsFinal(Status))
        {
            throw DomainException.ForField(
                "status",
                $"Lịch hẹn ở trạng thái “{AppointmentStatusText.Label(Status)}” thì không sửa được nữa.");
        }

        // BR-VAL-001 — giống lúc tạo: không dòng dịch vụ nào thì khoảng chiếm chỗ bằng 0,
        // và lịch hẹn trở thành một điểm vô nghĩa trên bảng giờ.
        if (services.Count == 0)
            throw DomainException.ForField("services", "Lịch hẹn phải có ít nhất một dịch vụ.");

        BranchId = Guard.Reference(branchId, "branchId", "Chi nhánh");
        CustomerId = Guard.Reference(customerId, "customerId", "Khách hàng");
        StaffId = Guard.Reference(staffId, "staffId", "Kỹ thuật viên");
        Source = source;
        Station = Guard.Optional(station, "station", "Ghế hoặc phòng", 80);
        Note = Guard.Optional(note, "note", "Ghi chú lịch hẹn", ValidationPolicy.LongTextMaxLength);
        Deposit = Guard.Money(deposit, "deposit", "Tiền cọc");

        StartAt = startAt;
        EndAt = startAt.AddMinutes(AppointmentSchedulePolicy.TotalMinutes(
            services.Select(service => (service.DurationMinutes, service.BufferMinutes))));

        _services.Clear();
        FillServices(services, lineIdFactory);

        UpdatedAt = now;
    }

    /// <summary>
    /// Dựng các dòng dịch vụ. Dùng chung cho lúc tạo và lúc sửa, để hai đường không thể chép
    /// khác nhau những trường mà <see cref="AppointmentService"/> phải giữ lại bản sao.
    /// </summary>
    private void FillServices(
        IReadOnlyList<(string ServiceId, string ServiceName, int DurationMinutes, int BufferMinutes)> services,
        Func<string> lineIdFactory)
    {
        foreach (var service in services)
        {
            _services.Add(AppointmentService.Create(
                lineIdFactory(),
                TenantId,
                Id,
                service.ServiceId,
                service.ServiceName,
                service.DurationMinutes,
                service.BufferMinutes));
        }
    }

    /// <summary>
    /// BR-APT-022 — chuyển trạng thái theo đúng sơ đồ ở mục 16.1; mọi chuyển đổi ngoài sơ
    /// đồ đều bị từ chối.
    /// <para>
    /// Không dùng hàm này để chuyển sang Completed: BR-APT-026 quy định việc đó chỉ xảy ra
    /// tự động khi hóa đơn được thanh toán đủ, hoặc theo ngoại lệ ở BR-APT-027.
    /// </para>
    /// </summary>
    public void ChangeStatus(AppointmentStatus next, DateTimeOffset now)
    {
        if (next == AppointmentStatus.Completed)
            throw DomainException.ForField("status", "Lịch hẹn chỉ hoàn tất khi hóa đơn đã thanh toán đủ.");

        if (!AppointmentLifecyclePolicy.CanTransition(Status, next))
        {
            throw DomainException.ForField(
                "status",
                $"Không thể chuyển lịch hẹn từ “{AppointmentStatusText.Label(Status)}” "
                + $"sang “{AppointmentStatusText.Label(next)}”.");
        }

        Status = next;
        UpdatedAt = now;
    }

    /// <summary>
    /// BR-APT-026 — hóa đơn gắn với lịch hẹn chuyển sang đã thanh toán thì lịch tự hoàn tất.
    /// <para>
    /// Hỏi <c>CanCompleteFromPayment</c> chứ không hỏi bảng chuyển trạng thái chung: đường
    /// này mở rộng hơn bảng ấy đúng một trạng thái, vì BR-INV-010 cho thu tiền cả lịch đang
    /// <c>CheckedIn</c> (quyết định 57).
    /// </para>
    /// <para>
    /// Lịch ở một trạng thái cuối — đã hủy, khách không đến — thì <b>người gọi phải hỏi luật
    /// trước</b> thay vì để lời gọi này ném lỗi: một lần thu tiền không được thất bại vì lịch
    /// hẹn nằm ở đâu, tiền khách đưa là có thật. Lỗi ở đây dành cho lập trình viên gọi sai chỗ.
    /// </para>
    /// </summary>
    public void CompleteFromPaidInvoice(DateTimeOffset now)
    {
        if (Status == AppointmentStatus.Completed) return;

        if (!AppointmentLifecyclePolicy.CanCompleteFromPayment(Status))
        {
            throw DomainException.ForField(
                "status",
                "Không thể hoàn tất lịch hẹn đang ở trạng thái "
                + $"“{AppointmentStatusText.Label(Status)}”.");
        }

        Status = AppointmentStatus.Completed;
        UpdatedAt = now;
    }

    /// <summary>
    /// BR-APT-027 — ngoại lệ: chủ tiệm được đóng lịch khi hóa đơn còn thiếu tiền. Lễ tân
    /// KHÔNG có quyền này; phép kiểm tra vai trò nằm ở tầng use case, còn ở đây chỉ ghi
    /// lại dấu vết để về sau đối chiếu được.
    /// <para>
    /// Hẹp hơn <see cref="CompleteFromPaidInvoice"/> đúng một trạng thái: chỉ đóng được buổi
    /// làm <b>đã bắt đầu</b>. Cờ <c>CompletedWithUnpaidBalance</c> là cách hệ thống "ghi chú
    /// hoàn tất khi chưa thu đủ" như BR-APT-027 đòi — nó đi thẳng ra DTO nên màn hình và báo
    /// cáo đều phân biệt được lịch này với một lịch hoàn tất bình thường.
    /// </para>
    /// </summary>
    public void CompleteWithUnpaidBalance(DateTimeOffset now)
    {
        if (!AppointmentLifecyclePolicy.CanForceComplete(Status))
        {
            throw DomainException.ForField(
                "status",
                "Chỉ đóng tay được lịch hẹn đang được phục vụ. "
                + $"Lịch này đang ở trạng thái “{AppointmentStatusText.Label(Status)}”.");
        }

        Status = AppointmentStatus.Completed;
        CompletedWithUnpaidBalance = true;
        UpdatedAt = now;
    }

    public void UpdateNote(string? note, string? station, DateTimeOffset now)
    {
        // BR-APT-023 — lịch hẹn đã hoàn tất thì không sửa được bất kỳ trường nào.
        if (Status == AppointmentStatus.Completed)
            throw DomainException.ForField("note", "Lịch hẹn đã hoàn tất thì không sửa được nữa.");

        Note = Guard.Optional(note, "note", "Ghi chú lịch hẹn", ValidationPolicy.LongTextMaxLength);
        Station = Guard.Optional(station, "station", "Ghế hoặc phòng", 80);
        UpdatedAt = now;
    }
}
