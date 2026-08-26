using NailManagement.Domain.Common;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Policies;

namespace NailManagement.Domain.Entities.Salon;

/// <summary>
/// Lịch hẹn của khách — trung tâm của nghiệp vụ tiệm nail.
/// <para>
/// Giờ kết thúc được LƯU thành cột chứ không tính lại mỗi lần đọc. Lý do là phép chống
/// trùng lịch ở BR-APT-011 phải chạy được bằng một câu truy vấn so sánh hai mốc thời gian;
/// nếu giờ kết thúc chỉ tồn tại trong bộ nhớ thì máy chủ buộc phải nạp toàn bộ lịch hẹn
/// của kỹ thuật viên lên rồi mới so được.
/// </para>
/// </summary>
public class Appointment : ITenantOwned
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
        foreach (var service in services)
        {
            var lineId = lineIdFactory?.Invoke() ?? $"{appointment.Id}-S{++index}";

            appointment._services.Add(AppointmentService.Create(
                lineId,
                tenantId,
                appointment.Id,
                service.ServiceId,
                service.ServiceName,
                service.DurationMinutes,
                service.BufferMinutes));
        }

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
            throw DomainException.ForField("status", $"Không thể chuyển lịch hẹn từ {Status} sang {next}.");

        Status = next;
        UpdatedAt = now;
    }

    /// <summary>BR-APT-026 — hóa đơn gắn với lịch hẹn chuyển sang đã thanh toán thì lịch tự hoàn tất.</summary>
    public void CompleteFromPaidInvoice(DateTimeOffset now)
    {
        if (Status == AppointmentStatus.Completed) return;

        if (!AppointmentLifecyclePolicy.CanTransition(Status, AppointmentStatus.Completed))
            throw DomainException.ForField("status", $"Không thể hoàn tất lịch hẹn đang ở trạng thái {Status}.");

        Status = AppointmentStatus.Completed;
        UpdatedAt = now;
    }

    /// <summary>
    /// BR-APT-027 — ngoại lệ: chủ tiệm được đóng lịch khi hóa đơn còn thiếu tiền. Lễ tân
    /// KHÔNG có quyền này; phép kiểm tra vai trò nằm ở tầng use case, còn ở đây chỉ ghi
    /// lại dấu vết để về sau đối chiếu được.
    /// </summary>
    public void CompleteWithUnpaidBalance(DateTimeOffset now)
    {
        CompleteFromPaidInvoice(now);
        CompletedWithUnpaidBalance = true;
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
