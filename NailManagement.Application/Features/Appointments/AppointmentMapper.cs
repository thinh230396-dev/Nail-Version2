using NailManagement.Domain.Common;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Policies;
using StaffEntity = NailManagement.Domain.Entities.Salon.Staff;

namespace NailManagement.Application.Features.Appointments;

/// <summary>
/// Chuyển entity <see cref="Appointment"/> sang DTO gửi ra ngoài, và đọc ngược các chuỗi
/// trạng thái cùng nguồn đặt lịch từ client.
/// </summary>
public static class AppointmentMapper
{
    /// <summary>BR-APT-020 — bảy trạng thái, không hơn.</summary>
    public static string ToWireFormat(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "PENDING",
        AppointmentStatus.Confirmed => "CONFIRMED",
        AppointmentStatus.CheckedIn => "CHECKED_IN",
        AppointmentStatus.InService => "IN_SERVICE",
        AppointmentStatus.Completed => "COMPLETED",
        AppointmentStatus.Cancelled => "CANCELLED",
        AppointmentStatus.NoShow => "NO_SHOW",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái lịch hẹn không hợp lệ.")
    };

    /// <summary>BR-APT-007 — bốn nguồn, do người nhập chọn tay.</summary>
    public static string ToWireFormat(AppointmentSource source) => source switch
    {
        AppointmentSource.Reception => "RECEPTION",
        AppointmentSource.Phone => "PHONE",
        AppointmentSource.Zalo => "ZALO",
        AppointmentSource.Online => "ONLINE",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Nguồn lịch hẹn không hợp lệ.")
    };

    public static AppointmentStatus ParseStatus(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "PENDING" => AppointmentStatus.Pending,
            "CONFIRMED" => AppointmentStatus.Confirmed,
            "CHECKED_IN" => AppointmentStatus.CheckedIn,
            "IN_SERVICE" => AppointmentStatus.InService,
            "COMPLETED" => AppointmentStatus.Completed,
            "CANCELLED" => AppointmentStatus.Cancelled,
            "NO_SHOW" => AppointmentStatus.NoShow,
            _ => throw DomainException.ForField(
                "status",
                "Trạng thái lịch hẹn chỉ nhận PENDING, CONFIRMED, CHECKED_IN, IN_SERVICE, "
                + "COMPLETED, CANCELLED hoặc NO_SHOW.")
        };

    /// <summary>
    /// BR-APT-021 — trạng thái khởi tạo. Rỗng thì hiểu là <c>PENDING</c>, và chỉ hai giá trị
    /// đó được phép; phép kiểm tra thật nằm trong <c>Appointment.Create</c>, ở đây chỉ dịch
    /// chuỗi và điền giá trị mặc định.
    /// </summary>
    public static AppointmentStatus ParseInitialStatus(string? value)
        => string.IsNullOrWhiteSpace(value) ? AppointmentStatus.Pending : ParseStatus(value);

    /// <summary>
    /// BR-APT-007 — rỗng thì hiểu là <c>RECEPTION</c>, vì phần lớn lịch hẹn của một tiệm nail
    /// sinh ra ngay tại quầy.
    /// </summary>
    public static AppointmentSource ParseSource(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "" => AppointmentSource.Reception,
            "RECEPTION" => AppointmentSource.Reception,
            "PHONE" => AppointmentSource.Phone,
            "ZALO" => AppointmentSource.Zalo,
            "ONLINE" => AppointmentSource.Online,
            _ => throw DomainException.ForField(
                "source", "Nguồn lịch hẹn chỉ nhận RECEPTION, PHONE, ZALO hoặc ONLINE.")
        };

    /// <summary>
    /// Dựng DTO từ lịch hẹn cùng hai bản ghi liên quan.
    /// <para>
    /// Khách và kỹ thuật viên được truyền vào chứ không đọc qua thuộc tính điều hướng, để
    /// nơi gọi buộc phải nạp chúng một cách có chủ đích. Đọc thẳng <c>appointment.Customer</c>
    /// ở đây là mở đường cho một truy vấn quên nạp rồi lặng lẽ trả ra một danh sách toàn tên
    /// rỗng, hoặc tệ hơn là nạp lẻ từng dòng.
    /// </para>
    /// </summary>
    /// <param name="now">
    /// Dùng cho đúng một việc: tính nhãn quá hạn ở BR §8 giả định 3. Truyền vào thay vì gọi
    /// đồng hồ ngay trong mapper, để mọi dòng của cùng một danh sách được so với cùng một mốc.
    /// </param>
    public static AppointmentDto ToDto(
        Appointment appointment, Customer customer, StaffEntity staff, DateTimeOffset now) => new(
        appointment.Id,
        appointment.TenantId,
        appointment.BranchId,
        appointment.CustomerId,
        customer.FullName,
        customer.Phone.Value,
        appointment.StaffId,
        staff.FullName,
        appointment.StartAt,
        appointment.EndAt,
        appointment.TotalMinutes(),
        ToWireFormat(appointment.Status),
        ToWireFormat(appointment.Source),
        appointment.Station,
        appointment.Note,
        appointment.Deposit,
        appointment.CompletedWithUnpaidBalance,
        IsOverdue(appointment, now),
        // Sắp theo tên dịch vụ chứ không theo thứ tự người dùng đã chọn: bảng
        // AppointmentServices không có cột thứ tự, nên thứ tự chèn không phải thứ mà một câu
        // SELECT hứa trả lại. Sắp theo tên thì cùng một lịch hẹn luôn đọc ra giống nhau ở
        // mọi màn hình, thay vì đổi chỗ sau một lần nạp lại mà không ai giải thích được.
        [.. appointment.Services
            .OrderBy(line => line.ServiceName, StringComparer.CurrentCulture)
            .Select(line => new AppointmentServiceLineDto(
                line.ServiceId, line.ServiceName, line.DurationMinutes, line.BufferMinutes))],
        [.. AppointmentLifecyclePolicy.NextStates(appointment.Status).Select(ToWireFormat)],
        appointment.CreatedAt,
        appointment.UpdatedAt);

    /// <summary>
    /// Dựng DTO từ một lịch hẹn <b>đã được nạp kèm</b> khách và kỹ thuật viên.
    /// <para>
    /// Dành cho hai đường đọc, nơi kho dữ liệu đã nối sẵn hai bảng đó trong cùng một câu truy
    /// vấn. Thiếu phép nối ấy là lỗi lập trình chứ không phải lỗi nghiệp vụ, nên nó ném ra
    /// <see cref="InvalidOperationException"/> kèm tên bảng còn thiếu — hỏng ngay và nói rõ,
    /// thay vì lặng lẽ trả về một danh sách toàn tên rỗng mà người dùng phải là người phát hiện.
    /// </para>
    /// </summary>
    public static AppointmentDto ToDto(Appointment appointment, DateTimeOffset now) => ToDto(
        appointment,
        appointment.Customer ?? throw NotLoaded(appointment.Id, nameof(Appointment.Customer)),
        appointment.Staff ?? throw NotLoaded(appointment.Id, nameof(Appointment.Staff)),
        now);

    private static InvalidOperationException NotLoaded(string appointmentId, string navigation)
        => new($"Lịch hẹn {appointmentId} được đọc mà chưa nạp kèm {navigation}. "
               + "Truy vấn ở tầng lưu trữ phải Include bảng này trước khi dựng DTO.");

    /// <summary>
    /// BR §8 giả định 3 — lịch còn chờ xác nhận mà giờ hẹn đã trôi qua thì mang nhãn quá hạn,
    /// <b>tính lúc đọc</b>. Không có job nền nào tự đổi trạng thái (BR-TENANT-003), và cũng
    /// không nên có: một lịch quá giờ mười phút thường là khách tới muộn, không phải khách
    /// không đến, và chỉ người ở quầy mới phân biệt được hai chuyện đó.
    /// <para>
    /// Chỉ áp cho <c>PENDING</c>. Lịch đã <c>CONFIRMED</c> mà trễ giờ là chuyện lễ tân đang
    /// theo dõi tại chỗ, còn bốn trạng thái sau thì khách đã tới rồi.
    /// </para>
    /// </summary>
    private static bool IsOverdue(Appointment appointment, DateTimeOffset now)
        => appointment.Status == AppointmentStatus.Pending && appointment.StartAt < now;
}
