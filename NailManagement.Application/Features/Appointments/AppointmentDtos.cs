namespace NailManagement.Application.Features.Appointments;

/// <summary>
/// Một dịch vụ trong lịch hẹn — BR-APT-003.
/// <para>
/// Mang tên, thời lượng và thời gian dọn dẹp đã được <b>chép lại lúc đặt lịch</b> chứ không
/// đọc ngược sang bảng dịch vụ. Đó là dữ liệu thật của dòng này: chủ tiệm sửa thời lượng một
/// dịch vụ hôm nay thì lịch hẹn tuần trước vẫn phải giữ nguyên giờ kết thúc mà nó đã chiếm.
/// </para>
/// <para>
/// Cố ý không có giá — BR-SVC-007 quy định lịch hẹn chưa hoàn tất lấy giá hiện tại của dịch
/// vụ khi lập hóa đơn, và giá chỉ được chốt trên dòng hóa đơn (BR-SVC-006).
/// </para>
/// </summary>
public sealed record AppointmentServiceLineDto(
    string ServiceId,
    string ServiceName,
    int DurationMinutes,
    int BufferMinutes);

/// <summary>
/// Một lịch hẹn của tiệm đang làm việc.
/// <para>
/// Mang kèm <paramref name="CustomerName"/>, <paramref name="CustomerPhone"/> và
/// <paramref name="StaffName"/> — <b>ngược quy ước của <see cref="StaffDto"/></b>, thứ chỉ
/// trả mã để màn hình tự ghép tên. Lý do giống <see cref="CustomerVisitDto"/> ở ngày 10: đây
/// là một bản đọc, và bảng lịch trong ngày phải hiện được tên khách cùng số điện thoại ngay
/// trên từng dòng. Bắt nó nạp trọn danh bạ khách của tiệm chỉ để dịch vài chục dòng là một
/// lời gọi mạng rất nặng cho một việc mà một phép nối đã làm xong. Mã định danh vẫn được
/// trả kèm, vì đó mới là thứ dùng khi bấm vào để mở hồ sơ.
/// </para>
/// <para>
/// Không có giá và không có tổng tiền: lịch hẹn không mang tiền dịch vụ (BR-SVC-006/007).
/// Thứ duy nhất liên quan tới tiền ở đây là <paramref name="Deposit"/> — BR-APT-030.
/// </para>
/// </summary>
/// <param name="TotalMinutes">
/// BR-APT-010 — tổng thời lượng cộng tổng thời gian dọn dẹp, tức đúng khoảng mà lịch hẹn
/// này chiếm chỗ của kỹ thuật viên. Trả sẵn để màn hình không phải cộng lại rồi ra một con
/// số khác con số máy chủ đã dùng để chống trùng.
/// </param>
/// <param name="IsOverdue">
/// Lịch còn ở <c>PENDING</c> mà giờ bắt đầu đã trôi qua. Tính <b>lúc đọc</b>, không có cột
/// nào trong database và không có gì tự đổi trạng thái — BR-TENANT-003 nói cả hệ thống không
/// có job chạy nền, nên đây chỉ là một cái nhãn để lễ tân biết cần gọi cho khách.
/// </param>
/// <param name="CompletedWithUnpaidBalance">
/// BR-APT-027 — lịch được chủ tiệm đóng tay khi hóa đơn còn thiếu tiền. Ở phạm vi ngày 11
/// giá trị này luôn là <c>false</c>: đường đi tới nó nằm ở lát cắt thu tiền.
/// </param>
public sealed record AppointmentDto(
    string Id,
    string TenantId,
    string BranchId,
    string CustomerId,
    string? CustomerName,
    string CustomerPhone,
    string StaffId,
    string StaffName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int TotalMinutes,
    string Status,
    string Source,
    string? Station,
    string? Note,
    long Deposit,
    bool CompletedWithUnpaidBalance,
    bool IsOverdue,
    IReadOnlyList<AppointmentServiceLineDto> Services,
    IReadOnlyList<string> NextStatuses,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Một điều đáng lưu ý nhưng <b>không chặn</b> việc lưu — BR-APT-005 và BR-APT-013.
/// <para>
/// Tách hẳn khỏi <c>fields</c> của contract lỗi, và đi kèm một phản hồi thành công chứ không
/// phải một phản hồi lỗi. Trộn hai thứ này là buộc màn hình phải đoán xem lần gọi vừa rồi đã
/// ghi được hay chưa: đặt lịch bù cho khách vừa làm xong là việc hằng ngày ở quầy, không
/// phải một lỗi cần sửa.
/// </para>
/// </summary>
/// <param name="Code"><c>APPOINTMENT_IN_PAST</c> hoặc <c>OUTSIDE_SHIFT</c>.</param>
public sealed record AppointmentWarning(string Code, string Message);

/// <summary>
/// Kết quả của một lệnh ghi lịch hẹn: bản ghi đã lưu, kèm những điều đáng lưu ý.
/// <para>
/// Luôn trả cả hai kể cả khi không có cảnh báo nào — một mảng rỗng là câu trả lời rõ ràng,
/// còn một trường lúc có lúc không thì mỗi màn hình sẽ tự phòng thủ một kiểu.
/// </para>
/// </summary>
public sealed record AppointmentSaveResult(
    AppointmentDto Appointment,
    IReadOnlyList<AppointmentWarning> Warnings);

/// <summary>
/// Một dịch vụ được chọn khi đặt lịch. Chỉ có mã: tên, thời lượng và thời gian dọn dẹp do
/// máy chủ đọc từ bảng dịch vụ rồi chép vào dòng lịch hẹn.
/// <para>
/// Nhận chúng từ client là để trình duyệt tự khai một buổi làm gel dài mười phút, và phép
/// chống trùng lịch ở BR-APT-011 sẽ tính trên con số do người đặt lịch tự đặt ra.
/// </para>
/// </summary>
public sealed record AppointmentServiceSelection(string ServiceId);

/// <summary>
/// Đặt một lịch hẹn.
/// <para>
/// Không có ô <c>tenantId</c> — tiệm đến từ phiên đăng nhập (BR-AUTH-024). <b>Cũng không có
/// ô <c>branchId</c></b>: BR-EMP-003 cho mỗi nhân viên đúng một chi nhánh, nên chi nhánh của
/// lịch hẹn là chi nhánh của kỹ thuật viên phụ trách. Nhận nó từ client là mở đường cho một
/// lịch hẹn ghi ở Quận 1 trong khi người làm ngồi ở Quận 3.
/// </para>
/// <para>
/// Không có ô <c>endAt</c>: giờ kết thúc suy ra từ danh sách dịch vụ theo BR-APT-010.
/// </para>
/// </summary>
/// <param name="Status">
/// BR-APT-021 — <c>PENDING</c> hoặc <c>CONFIRMED</c>. Rỗng thì hiểu là <c>PENDING</c>. Lễ tân
/// tiếp khách ngay tại quầy tạo thẳng ở <c>CONFIRMED</c> vì không còn gì phải chờ xác nhận.
/// </param>
/// <param name="Source">BR-APT-007 — <c>RECEPTION</c>, <c>PHONE</c>, <c>ZALO</c> hoặc <c>ONLINE</c>, do người nhập chọn tay.</param>
public sealed record CreateAppointmentCommand(
    string CustomerId,
    string StaffId,
    DateTimeOffset StartAt,
    IReadOnlyList<AppointmentServiceSelection> Services,
    string? Status,
    string? Source,
    string? Station,
    string? Note,
    long Deposit);

/// <summary>
/// Sửa trọn một lịch hẹn — cùng khuôn thay-trọn với <see cref="UpdateStaffCommand"/> và
/// <see cref="UpdateCustomerCommand"/>: bỏ trống ghi chú là xóa ghi chú.
/// <para>
/// Không có ô <c>status</c>. Đổi trạng thái đi đường riêng vì nó có sơ đồ chuyển và bảng
/// phân quyền của mình (BR-APT-022); để nó lẫn vào biểu mẫu sửa là mời người ta nhảy thẳng
/// từ <c>PENDING</c> sang <c>NO_SHOW</c> chỉ bằng cách lưu lại một lần.
/// </para>
/// </summary>
public sealed record UpdateAppointmentCommand(
    string AppointmentId,
    string CustomerId,
    string StaffId,
    DateTimeOffset StartAt,
    IReadOnlyList<AppointmentServiceSelection> Services,
    string? Source,
    string? Station,
    string? Note,
    long Deposit);

/// <summary>
/// BR-APT-025 — dời lịch là sửa giờ bắt đầu, giữ nguyên trạng thái và giữ nguyên độ dài.
/// <para>
/// Có endpoint riêng thay vì bắt gọi lệnh sửa trọn, vì đây là thao tác kéo thả một ô trên
/// bảng giờ: gửi trọn hồ sơ chỉ để đổi mỗi giờ bắt đầu là mở ra khả năng ghi đè nhầm những
/// trường mà người dùng không hề chạm tới. Phạm vi cũng hẹp hơn — chỉ dời được lịch còn ở
/// <c>PENDING</c> hoặc <c>CONFIRMED</c>, trong khi lệnh sửa trọn còn cho tới trước ba trạng
/// thái cuối.
/// </para>
/// </summary>
public sealed record RescheduleAppointmentCommand(string AppointmentId, DateTimeOffset StartAt);

/// <param name="Status">
/// Một trong bảy trạng thái ở BR-APT-020. Đích đến phải nằm trong sơ đồ §16.1, nếu không thì
/// bị từ chối — BR-APT-022.
/// </param>
public sealed record ChangeAppointmentStatusCommand(string AppointmentId, string Status);
