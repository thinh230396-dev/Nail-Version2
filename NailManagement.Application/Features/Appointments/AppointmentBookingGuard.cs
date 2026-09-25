using System.Globalization;
using NailManagement.Application.Common;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Domain.Salon.Appointments;
using NailManagement.Domain.Salon.Branches;
using NailManagement.Domain.Salon.Customers;
using NailManagement.Domain.Salon.Services;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.Shared;

using StaffEntity = NailManagement.Domain.Salon.StaffMembers.Staff;

namespace NailManagement.Application.Features.Appointments;

/// <summary>
/// Những phép kiểm tra mà <b>cả ba đường ghi lịch hẹn</b> đều phải chạy: đặt mới, sửa trọn,
/// và dời giờ.
/// <para>
/// Tách thành một lớp dùng chung vì đây chính là bài học đã trả giá trong dự án này: ngày 5
/// đường "bật lại chi nhánh" quên kiểm hạn mức, và ngày 7 suýt lặp lại ở đường "nhận lại nhân
/// viên". Chống trùng lịch nằm trong bốn thứ mà lộ trình đánh dấu tuyệt đối không cắt, nên nó
/// không được có ba bản sao ở ba use case để rồi lệch nhau sau một lần ai đó sửa một chỗ.
/// </para>
/// <para>
/// Ranh giới của lớp này: nó <b>đọc và kiểm tra</b>, không ghi gì cả. Việc dựng entity và lưu
/// xuống vẫn nằm ở từng use case.
/// </para>
/// </summary>
public sealed class AppointmentBookingGuard(
    IAppointmentRepository appointments,
    ICustomerRepository customers,
    IStaffRepository staffMembers,
    IServiceRepository services,
    IBranchRepository branches)
{
    /// <summary>
    /// Định dạng giờ trong thông báo trùng lịch. Đọc theo giờ ghi trên chính bản ghi — thứ mà
    /// kiểu <c>datetimeoffset</c> giữ nguyên — chứ không quy về UTC, để lễ tân thấy đúng con
    /// số họ nhìn trên đồng hồ treo tường.
    /// </summary>
    private const string ClockFormat = "HH:mm";

    private const string DayFormat = "dd/MM";

    /// <summary>
    /// BR-CUS-004 — mọi lịch hẹn bắt buộc gắn một hồ sơ khách, không có khách vãng lai ẩn danh.
    /// <para>
    /// Không chặn khách đã ngừng hoạt động: BR-CUS-006 nói ngừng là "xóa" dưới mắt giao diện,
    /// nhưng một hồ sơ vừa bị tắt nhầm trong khi khách đang đứng ở quầy thì việc cần làm là
    /// bật lại, không phải chặn người ta đặt lịch. Danh sách chọn khách ở màn hình mới là nơi lọc.
    /// </para>
    /// </summary>
    public async Task<Customer> ResolveCustomerAsync(
        string? customerId, CancellationToken cancellationToken = default)
        => await customers.FindByIdAsync(customerId ?? string.Empty, cancellationToken)
           ?? throw new NotFoundException("Không tìm thấy khách hàng cho lịch hẹn này.");

    /// <summary>
    /// BR-APT-004 — đúng một kỹ thuật viên phụ trách toàn bộ lịch hẹn, và người đó phải nằm
    /// trong phạm vi chi nhánh của người đang thao tác (BR-APT-002).
    /// <para>
    /// Chặn người đã nghỉ việc, khác cách xử với khách hàng ở trên: một hồ sơ nhân viên
    /// <c>INACTIVE</c> nghĩa là người đó không còn đến tiệm nữa, nên xếp lịch cho họ là xếp
    /// một buổi chắc chắn không có ai làm.
    /// </para>
    /// </summary>
    public async Task<StaffEntity> ResolveStaffAsync(
        string? staffId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var staff = await staffMembers.FindByIdAsync(staffId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kỹ thuật viên cho lịch hẹn này.");

        BranchScope.EnsureInScope(staff, actor, "Không tìm thấy kỹ thuật viên này trong chi nhánh của bạn.");

        if (staff.Role != StaffRole.Technician)
            throw DomainException.ForField("staffId", "Người phụ trách lịch hẹn phải là kỹ thuật viên.");

        if (staff.Status == StaffStatus.Inactive)
        {
            throw DomainException.ForField(
                "staffId", $"{staff.FullName} đã nghỉ việc nên không nhận lịch hẹn mới được.");
        }

        return staff;
    }

    /// <summary>BR-BRANCH-004: chỉ chặn tạo lịch mới; không thay đổi quyền xem/sửa dữ liệu cũ.</summary>
    public async Task<StaffEntity> ResolveStaffForNewAppointmentAsync(
        string? staffId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var staff = await ResolveStaffAsync(staffId, actor, cancellationToken);
        var branch = await branches.FindByIdAsync(staff.BranchId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chi nhánh của kỹ thuật viên.");
        if (branch.Status != BranchStatus.Active)
            throw DomainException.ForField("staffId", "Chi nhánh của kỹ thuật viên đã ngừng hoạt động nên không nhận lịch hẹn mới.");
        return staff;
    }

    /// <summary>
    /// Đọc bảng dịch vụ rồi trả về đúng bộ số mà lịch hẹn cần chép lại — BR-APT-003 và
    /// BR-APT-010.
    /// <para>
    /// Thời lượng và thời gian dọn dẹp lấy từ <b>máy chủ</b>, không lấy từ thân request. Nhận
    /// chúng của client là để trình duyệt tự khai một buổi làm gel dài mười phút, rồi phép
    /// chống trùng lịch sẽ tính trên con số do chính người đặt lịch đặt ra.
    /// </para>
    /// <para>
    /// Cho phép cùng một dịch vụ xuất hiện nhiều lần: khách làm hai bộ móng trong một lượt là
    /// chuyện có thật, và mỗi lần đều chiếm thêm thời gian của kỹ thuật viên.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<(string ServiceId, string ServiceName, int DurationMinutes, int BufferMinutes)>>
        ResolveServicesAsync(
            IReadOnlyList<AppointmentServiceSelection>? selections,
            CancellationToken cancellationToken = default)
    {
        // BR-APT-003 — bắt buộc ít nhất một dòng. Kiểm ở đây để lỗi gắn vào ô chọn dịch vụ của
        // biểu mẫu; Appointment.Create vẫn kiểm lại lần nữa cho ai gọi thẳng vào entity.
        if (selections is null || selections.Count == 0)
            throw DomainException.ForField("services", "Lịch hẹn phải có ít nhất một dịch vụ.");

        var catalogue = await services.ListAsync(cancellationToken);
        var byId = catalogue.ToDictionary(service => service.Id);

        var resolved = new List<(string, string, int, int)>(selections.Count);

        foreach (var selection in selections)
        {
            if (!byId.TryGetValue(selection.ServiceId ?? string.Empty, out var service))
            {
                throw DomainException.ForField(
                    "services", $"Không tìm thấy dịch vụ {selection.ServiceId} trong bảng giá của tiệm.");
            }

            // BR-SVC-005 — dịch vụ đã ngừng bán thì không đặt được nữa, nhưng lịch hẹn cũ vẫn
            // giữ nguyên tên của nó (BR-DEL-003) nhờ bản chép nằm trên dòng lịch hẹn.
            if (service.Status == ServiceStatus.Inactive)
            {
                throw DomainException.ForField(
                    "services", $"Dịch vụ “{service.Name}” đã ngừng bán nên không đặt lịch được.");
            }

            resolved.Add((service.Id, service.Name, service.DurationMinutes, service.BufferMinutes));
        }

        return resolved;
    }

    /// <summary>
    /// BR-APT-011 — chặn cứng nếu kỹ thuật viên đã có lịch chồng giờ, rồi trả về những điều
    /// đáng lưu ý nhưng không chặn.
    /// <para>
    /// Hai việc đi cùng một hàm là có chủ đích: chúng cùng nhìn vào một khoảng thời gian và
    /// một con người, và tách ra thì sớm muộn có đường ghi chỉ gọi một trong hai. Thứ tự cũng
    /// quan trọng — chặn trước, cảnh báo sau: không có lý do gì phải dựng câu cảnh báo cho một
    /// lịch hẹn sắp bị từ chối.
    /// </para>
    /// </summary>
    /// <param name="exceptAppointmentId">Chính lịch hẹn đang sửa hoặc đang dời, để nó không tự trùng giờ với mình.</param>
    public async Task<IReadOnlyList<AppointmentWarning>> EnsureSlotAvailableAsync(
        StaffEntity staff,
        DateTimeOffset start,
        DateTimeOffset end,
        string? exceptAppointmentId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var blocking = await appointments.FindBlockingAsync(
            staff.Id, start, end, exceptAppointmentId, cancellationToken);

        if (blocking is not null) throw new SlotConflictException(DescribeConflict(staff, blocking));

        var warnings = new List<AppointmentWarning>(2);

        // BR-APT-005 — đặt lịch trong quá khứ chỉ cảnh báo. Lễ tân cần ghi bù cho khách vừa
        // làm xong, và chặn việc đó là đẩy họ ra khỏi phần mềm.
        if (start < now)
        {
            warnings.Add(new AppointmentWarning(
                "APPOINTMENT_IN_PAST",
                "Giờ hẹn đã trôi qua. Lịch vẫn được lưu — dùng khi cần ghi bù cho khách vừa làm xong."));
        }

        // BR-APT-013 — ngoài ca cũng chỉ cảnh báo. BR-APT-014 nói rõ hệ thống không kiểm giờ mở
        // cửa chi nhánh và không kiểm ngày nghỉ lễ, nên đây là phép kiểm thời gian duy nhất
        // ngoài phép chống trùng.
        if (IsOutsideShift(staff, start, end))
        {
            warnings.Add(new AppointmentWarning(
                "OUTSIDE_SHIFT",
                $"Giờ hẹn nằm ngoài ca của {staff.FullName} "
                + $"({staff.ShiftStart.ToString(ClockFormat, CultureInfo.InvariantCulture)}"
                + $"–{staff.ShiftEnd.ToString(ClockFormat, CultureInfo.InvariantCulture)}). "
                + "Lịch vẫn được lưu."));
        }

        return warnings;
    }

    /// <summary>
    /// Câu chữ của lỗi trùng lịch. Phải nói đủ <b>ai đang giữ chỗ, từ mấy giờ tới mấy giờ,
    /// ngày nào, và cho khách nào</b>: contract lỗi chỉ có ba trường và không mang được một
    /// đối tượng đính kèm, nên toàn bộ thứ lễ tân cần để xếp lại lịch phải nằm trong câu này.
    /// </summary>
    private static string DescribeConflict(StaffEntity staff, Appointment blocking)
    {
        var customerName = blocking.Customer?.FullName
            ?? blocking.Customer?.Phone.Value
            ?? "khách khác";

        return $"{staff.FullName} đã có lịch "
               + $"{blocking.StartAt.ToString(ClockFormat, CultureInfo.InvariantCulture)}"
               + $"–{blocking.EndAt.ToString(ClockFormat, CultureInfo.InvariantCulture)} "
               + $"ngày {blocking.StartAt.ToString(DayFormat, CultureInfo.InvariantCulture)} "
               + $"với {customerName}. Chọn giờ khác hoặc đổi người làm.";
    }

    /// <summary>
    /// So giờ hẹn với ca cố định của nhân viên (BR-EMP-009), theo <b>giờ ghi trên bản ghi</b>
    /// chứ không quy về UTC: <c>ShiftStart</c> và <c>ShiftEnd</c> là giờ đồng hồ treo tường của
    /// tiệm, nên phải so với cùng một loại giờ.
    /// <para>
    /// Lịch vắt qua nửa đêm được tính thẳng là ngoài ca. Không ca nào của một tiệm nail chứa
    /// được nửa đêm, và nếu so bằng <c>TimeOnly</c> thì giờ kết thúc sẽ nhỏ hơn giờ bắt đầu rồi
    /// phép so trả về "trong ca" — một câu trả lời sai theo hướng im lặng.
    /// </para>
    /// </summary>
    private static bool IsOutsideShift(StaffEntity staff, DateTimeOffset start, DateTimeOffset end)
        => end.Date != start.Date
           || AppointmentSchedulePolicy.IsOutsideShift(
               staff.ShiftStart,
               staff.ShiftEnd,
               TimeOnly.FromTimeSpan(start.TimeOfDay),
               TimeOnly.FromTimeSpan(end.TimeOfDay));
}
