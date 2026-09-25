using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.Common;
using NailManagement.Application.Features.Appointments;
using NailManagement.Application.Features.Appointments.UseCases;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.API.Controllers;

/// <summary>Một dịch vụ được chọn khi đặt lịch — chỉ có mã, mọi thứ còn lại máy chủ tự đọc.</summary>
public sealed record AppointmentServiceRequest(string? ServiceId);

/// <summary>
/// Thân request của cả <c>POST</c> lẫn <c>PUT</c>.
/// <para>
/// Không có <c>branchId</c>: chi nhánh của lịch hẹn là chi nhánh của kỹ thuật viên phụ trách
/// (BR-EMP-003). Không có <c>endAt</c> và không có <c>duration</c>: giờ kết thúc suy ra từ
/// danh sách dịch vụ theo BR-APT-010 — nhận nó từ client là để trình duyệt tự quyết định một
/// buổi hẹn chiếm bao nhiêu chỗ, và phép chống trùng lịch sẽ tính trên con số đó.
/// </para>
/// <para>
/// <c>Status</c> chỉ có nghĩa ở <c>POST</c> (BR-APT-021, hai giá trị đầu). Ở <c>PUT</c> nó
/// được bỏ qua: đổi trạng thái đi đường riêng vì có sơ đồ chuyển của mình (BR-APT-022).
/// </para>
/// </summary>
public sealed record SaveAppointmentRequest(
    string? CustomerId,
    string? StaffId,
    DateTimeOffset StartAt,
    IReadOnlyList<AppointmentServiceRequest>? Services,
    string? Status,
    string? Source,
    string? Station,
    string? Note,
    long Deposit);

public sealed record RescheduleAppointmentRequest(DateTimeOffset StartAt);

public sealed record ChangeAppointmentStatusRequest(string? Status);

/// <summary>
/// Lịch hẹn của tiệm đang làm việc.
/// <para>
/// Không endpoint nào nhận mã tiệm, và cũng không endpoint nào nhận mã chi nhánh để lọc: chi
/// nhánh của lễ tân đến từ phiên đăng nhập qua <c>ActorContext.BranchId</c> (BR-EMP-004).
/// Nhận nó từ chuỗi truy vấn là để lễ tân tự khai mình thuộc chi nhánh nào, và ô "chỉ thao
/// tác lịch hẹn chi nhánh mình" ở BR-APT-002 sẽ chỉ còn là một quy ước của giao diện.
/// </para>
/// <para>
/// Cả chủ tiệm lẫn lễ tân đều có ô <c>Full</c> ở nhóm <c>Appointments</c> trong ma trận mục
/// 3.4 — xếp lịch là công việc hằng ngày ở quầy. Superadmin <b>không có ô nào</b>: BR-AUTH-030
/// và BR-APT-001 đều nói rõ họ không chạm được vào dữ liệu nghiệp vụ bên trong tiệm.
/// </para>
/// <para>
/// Cố ý KHÔNG có động từ <c>DELETE</c>. BR-APT-024 quy định không có chức năng xóa lịch hẹn;
/// thứ mà giao diện gọi là hủy lịch chính là <c>PATCH /{id}/status</c> với <c>CANCELLED</c>.
/// </para>
/// </summary>
[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController(
    ListAppointmentsUseCase listAppointments,
    GetAppointmentUseCase getAppointment,
    CreateAppointmentUseCase createAppointment,
    UpdateAppointmentUseCase updateAppointment,
    RescheduleAppointmentUseCase rescheduleAppointment,
    ChangeAppointmentStatusUseCase changeAppointmentStatus,
    RequestScope requestScope) : ControllerBase
{
    /// <summary>
    /// Bảng lịch trong một khoảng ngày. Thiếu tham số thì lấy hôm nay theo giờ Việt Nam.
    /// <para>
    /// Là endpoint đọc đầu tiên có khoảng ngày, khác bốn module trước vốn trả trọn danh sách:
    /// lịch hẹn cộng dồn mãi theo thời gian, trong khi màn hình chỉ bao giờ hiện một ngày hoặc
    /// một tuần.
    /// </para>
    /// </summary>
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.Appointments)]
    public async Task<IActionResult> List(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
        => Ok(new { appointments = await listAppointments.ExecuteAsync(from, to, Actor(), cancellationToken) });

    [HttpGet("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.Appointments)]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
        => Ok(new { appointment = await getAppointment.ExecuteAsync(id, Actor(), cancellationToken) });

    /// <summary>
    /// Đặt lịch — BR-APT-001.
    /// <para>
    /// Trả kèm <c>warnings</c>: đặt trong quá khứ (BR-APT-005) và đặt ngoài ca (BR-APT-013) là
    /// những điều đáng nói nhưng <b>không chặn</b>, nên chúng đi cùng một phản hồi 201 chứ
    /// không phải một phản hồi lỗi. Trùng giờ kỹ thuật viên thì ngược lại: 409
    /// <c>SLOT_CONFLICT</c>, chặn cứng theo BR-APT-011.
    /// </para>
    /// </summary>
    [HttpPost]
    [RequireAuth]
    [RequirePermission(Feature.Appointments, Write = true)]
    public async Task<IActionResult> Create(
        [FromBody] SaveAppointmentRequest? request, CancellationToken cancellationToken)
    {
        var result = await createAppointment.ExecuteAsync(
            new CreateAppointmentCommand(
                request?.CustomerId ?? string.Empty,
                request?.StaffId ?? string.Empty,
                request?.StartAt ?? default,
                ToSelections(request?.Services),
                request?.Status,
                request?.Source,
                request?.Station,
                request?.Note,
                request?.Deposit ?? 0L),
            Actor(),
            cancellationToken);

        return Created(
            $"/api/appointments/{result.Appointment.Id}",
            new { appointment = result.Appointment, warnings = result.Warnings });
    }

    /// <summary>
    /// Sửa trọn lịch hẹn, cùng khuôn thay-trọn với <c>PUT /api/staff/{id}</c> và
    /// <c>PUT /api/customers/{id}</c>: bỏ trống ghi chú trong thân request là xóa ghi chú, nên
    /// biểu mẫu sửa phải gửi đủ mọi trường kể cả những trường người dùng không đụng tới.
    /// </summary>
    [HttpPut("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.Appointments, Write = true)]
    public async Task<IActionResult> Update(
        string id, [FromBody] SaveAppointmentRequest? request, CancellationToken cancellationToken)
    {
        var result = await updateAppointment.ExecuteAsync(
            new UpdateAppointmentCommand(
                id,
                request?.CustomerId ?? string.Empty,
                request?.StaffId ?? string.Empty,
                request?.StartAt ?? default,
                ToSelections(request?.Services),
                request?.Source,
                request?.Station,
                request?.Note,
                request?.Deposit ?? 0L),
            Actor(),
            cancellationToken);

        return Ok(new { appointment = result.Appointment, warnings = result.Warnings });
    }

    /// <summary>
    /// BR-APT-025 — dời lịch, giữ nguyên trạng thái và độ dài. Đường riêng cho thao tác kéo
    /// thả trên bảng giờ, nơi không có biểu mẫu nào để đọc lại các trường còn lại trước khi gửi.
    /// </summary>
    [HttpPatch("{id}/schedule")]
    [RequireAuth]
    [RequirePermission(Feature.Appointments, Write = true)]
    public async Task<IActionResult> Reschedule(
        string id, [FromBody] RescheduleAppointmentRequest? request, CancellationToken cancellationToken)
    {
        var result = await rescheduleAppointment.ExecuteAsync(
            new RescheduleAppointmentCommand(id, request?.StartAt ?? default),
            Actor(),
            cancellationToken);

        return Ok(new { appointment = result.Appointment, warnings = result.Warnings });
    }

    /// <summary>
    /// BR-APT-022 — chuyển trạng thái theo sơ đồ mục 16.1. Đây cũng là đường hủy lịch
    /// (BR-APT-024) và đường ghi nhận khách không đến.
    /// <para>
    /// Chuyển sang <c>COMPLETED</c> là ngoại lệ duy nhất, và nó <b>chỉ dành cho chủ tiệm</b>:
    /// BR-APT-026 nói lịch tự hoàn tất khi hóa đơn thu đủ tiền — đường đó nằm ở lệnh thu tiền,
    /// không ai bấm nút — còn BR-APT-027 cho chủ tiệm đóng tay một lịch đang phục vụ dở khi
    /// hóa đơn chưa thu đủ. Lễ tân gửi <c>COMPLETED</c> sẽ nhận 403 kèm câu chữ nói rõ lịch tự
    /// hoàn tất khi thu đủ tiền.
    /// </para>
    /// <para>
    /// Phép kiểm quyền ấy nằm trong use case chứ không ở thuộc tính <c>RequirePermission</c>
    /// bên dưới, vì nó phụ thuộc vào nội dung thân request: cùng đường dẫn này còn là đường
    /// hủy lịch mà lễ tân dùng cả ngày.
    /// </para>
    /// </summary>
    [HttpPatch("{id}/status")]
    [RequireAuth]
    [RequirePermission(Feature.Appointments, Write = true)]
    public async Task<IActionResult> ChangeStatus(
        string id, [FromBody] ChangeAppointmentStatusRequest? request, CancellationToken cancellationToken)
    {
        var appointment = await changeAppointmentStatus.ExecuteAsync(
            new ChangeAppointmentStatusCommand(id, request?.Status ?? string.Empty),
            Actor(),
            cancellationToken);

        return Ok(new { appointment });
    }

    private static IReadOnlyList<AppointmentServiceSelection> ToSelections(
        IReadOnlyList<AppointmentServiceRequest>? services)
        => [.. (services ?? []).Select(service => new AppointmentServiceSelection(service.ServiceId ?? string.Empty))];

    /// <summary>
    /// Người thực hiện, dựng từ phiên đăng nhập chứ không từ thân request. Ở controller này nó
    /// mang theo chi nhánh — thứ quyết định lễ tân nhìn thấy và sửa được lịch hẹn của ai.
    /// </summary>
    private ActorContext Actor()
        => requestScope.ToActor(HttpContext.Connection.RemoteIpAddress?.ToString());
}
