using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.Common;
using NailManagement.Application.Features.SalesInvoices;
using NailManagement.Application.Features.SalesInvoices.UseCases;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.API.Controllers;

/// <summary>
/// Một dòng người dùng gửi lên. Gửi <c>serviceId</c> thì máy chủ tự đọc tên và giá hiện tại;
/// gửi <c>name</c> và <c>unitPrice</c> thì đó là mục nhập tay (BR-INV-012).
/// </summary>
public sealed record SalesInvoiceLineRequest(
    string? ServiceId,
    string? Name,
    long UnitPrice,
    int Quantity);

/// <summary>
/// Thân request lập hóa đơn.
/// <para>
/// Không có <c>code</c> — số hóa đơn do máy chủ cấp trong giao dịch (BR-INV-016). Không có
/// <c>status</c> — trạng thái suy ra từ tổng thu (BR-PAY-003). Nhận bất kỳ cái nào trong hai
/// trường đó là mở đường cho một hóa đơn tự khai mình đã thanh toán.
/// </para>
/// </summary>
public sealed record CreateSalesInvoiceRequest(
    string? AppointmentId,
    string? CustomerId,
    string? StaffId,
    string? BranchId,
    IReadOnlyList<SalesInvoiceLineRequest>? Lines,
    long Discount,
    string? DiscountReason,
    long Tip,
    string? Note);

public sealed record UpdateSalesInvoiceRequest(
    string? StaffId,
    IReadOnlyList<SalesInvoiceLineRequest>? Lines,
    long Discount,
    string? DiscountReason,
    long Tip,
    string? Note);

public sealed record ChangeSalesInvoiceStatusRequest(string? Status);

/// <summary>
/// Một lần khách trả tiền.
/// <para>
/// Không có <c>type</c> — đường này luôn sinh dòng <c>PAYMENT</c> (BR-PAY-002). Không có
/// <c>paidAt</c> — giờ thu là giờ máy chủ, vì doanh thu ở BR-REV-001 đếm theo tiền thực thu
/// và nhận mốc thời gian từ client là cho phép dời một khoản thu sang tháng khác.
/// </para>
/// </summary>
public sealed record RecordPaymentRequest(string? Method, long Amount, string? Reference);

/// <param name="Reason">Bắt buộc — BR-PAY-006.</param>
public sealed record IssueRefundRequest(string? Method, long Amount, string? Reason);

/// <summary>
/// Hóa đơn bán hàng — BR-INV-001, thứ <b>khách trả cho tiệm</b>.
/// <para>
/// ⚠️ Đường dẫn là <c>/api/sales-invoices</c> chứ không phải <c>/api/invoices</c>, và đó là chủ
/// đích: hệ thống có hai bảng hóa đơn tách hoàn toàn, và bảng kia — hóa đơn đăng ký tiệm trả cho
/// SalonSys — thuộc quyền Superadmin. Một đường dẫn mơ hồ ở đây là mời người đọc mã nhầm hai thứ
/// đó với nhau, mà nhầm chúng là nhầm luôn ý nghĩa của mọi con số doanh thu.
/// </para>
/// <para>
/// Cả chủ tiệm lẫn lễ tân đều có ô <c>Full</c> ở nhóm <c>SalesInvoices</c> trong ma trận mục
/// 3.4 — thu tiền là công việc hằng ngày ở quầy. Superadmin <b>không có ô nào</b> (BR-AUTH-030).
/// Lễ tân chỉ thấy hóa đơn chi nhánh mình (BR-ISO-004), khác hẳn danh bạ khách mà họ thấy cả tiệm.
/// </para>
/// <para>
/// Bảy endpoint, đúng ngân sách §9.1. Hai cái cuối — thu tiền và hoàn tiền — <b>không dùng
/// chung nhóm quyền</b>: thu tiền là việc hằng ngày của quầy, còn hoàn tiền mang nhóm
/// <c>Refunds</c> mà lễ tân cố ý vắng mặt (BR-PAY-007). Cùng một tài nguyên, hai mức quyền
/// khác nhau, vì tiền vào và tiền ra không phải hai chiều của cùng một thao tác.
/// </para>
/// </summary>
[ApiController]
[Route("api/sales-invoices")]
public sealed class SalesInvoicesController(
    ListSalesInvoicesUseCase listInvoices,
    GetSalesInvoiceUseCase getInvoice,
    CreateSalesInvoiceUseCase createInvoice,
    UpdateSalesInvoiceUseCase updateInvoice,
    ChangeSalesInvoiceStatusUseCase changeInvoiceStatus,
    RecordPaymentUseCase recordPayment,
    IssueRefundUseCase issueRefund,
    RequestScope requestScope) : ControllerBase
{
    /// <summary>Sổ hóa đơn trong một khoảng ngày. Thiếu tham số thì lấy hôm nay theo giờ tiệm.</summary>
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.SalesInvoices)]
    public async Task<IActionResult> List(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
        => Ok(new { invoices = await listInvoices.ExecuteAsync(from, to, Actor(), cancellationToken) });

    [HttpGet("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.SalesInvoices)]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
        => Ok(new { invoice = await getInvoice.ExecuteAsync(id, Actor(), cancellationToken) });

    /// <summary>
    /// Lập hóa đơn: gửi <c>appointmentId</c> thì đây là lệnh "Thanh toán" trên một lịch hẹn
    /// (BR-INV-010); bỏ trống thì đây là hóa đơn bán lẻ (BR-INV-011) và <c>lines</c> là bắt buộc.
    /// </summary>
    [HttpPost]
    [RequireAuth]
    [RequirePermission(Feature.SalesInvoices, Write = true)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSalesInvoiceRequest? request, CancellationToken cancellationToken)
    {
        var invoice = await createInvoice.ExecuteAsync(
            new CreateSalesInvoiceCommand(
                request?.AppointmentId,
                request?.CustomerId,
                request?.StaffId,
                request?.BranchId,
                ToLineInputs(request?.Lines),
                request?.Discount ?? 0L,
                request?.DiscountReason,
                request?.Tip ?? 0L,
                request?.Note),
            Actor(),
            cancellationToken);

        return Created($"/api/sales-invoices/{invoice.Id}", new { invoice });
    }

    /// <summary>
    /// BR-INV-015 — sửa trọn một hóa đơn chưa thu đủ. Hóa đơn đã thanh toán bị từ chối theo
    /// BR-INV-014; sai sót ở đó chỉ xử lý bằng hoàn tiền.
    /// </summary>
    [HttpPut("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.SalesInvoices, Write = true)]
    public async Task<IActionResult> Update(
        string id, [FromBody] UpdateSalesInvoiceRequest? request, CancellationToken cancellationToken)
    {
        var invoice = await updateInvoice.ExecuteAsync(
            new UpdateSalesInvoiceCommand(
                id,
                request?.StaffId,
                ToLineInputs(request?.Lines),
                request?.Discount ?? 0L,
                request?.DiscountReason,
                request?.Tip ?? 0L,
                request?.Note),
            Actor(),
            cancellationToken);

        return Ok(new { invoice });
    }

    /// <summary>
    /// BR-INV-015 — hủy hóa đơn, và ở lát cắt này là chuyển trạng thái duy nhất đặt được bằng tay.
    /// <para>
    /// Cố ý KHÔNG có động từ <c>DELETE</c>: BR-DEL-001 không cho xóa cứng thứ gì, và một số hóa
    /// đơn đã cấp thì phải tra lại được kể cả khi hóa đơn bị hủy.
    /// </para>
    /// </summary>
    [HttpPatch("{id}/status")]
    [RequireAuth]
    [RequirePermission(Feature.SalesInvoices, Write = true)]
    public async Task<IActionResult> ChangeStatus(
        string id, [FromBody] ChangeSalesInvoiceStatusRequest? request, CancellationToken cancellationToken)
    {
        var invoice = await changeInvoiceStatus.ExecuteAsync(
            new ChangeSalesInvoiceStatusCommand(id, request?.Status ?? string.Empty),
            Actor(),
            cancellationToken);

        return Ok(new { invoice });
    }

    /// <summary>
    /// Ghi nhận một lần khách trả tiền — BR-PAY-001, và là lệnh kéo theo nhiều hệ quả nhất
    /// trong toàn hệ thống.
    /// <para>
    /// Một lần gọi làm ba việc trong <b>một giao dịch</b>: thêm dòng thu, hóa đơn tự tính lại
    /// trạng thái từ tổng thu (BR-PAY-003), và nếu vừa thu đủ thì lịch hẹn gắn với nó tự hoàn
    /// tất (BR-APT-026). Phản hồi là hóa đơn sau khi đã cộng xong, nên màn hình quầy chỉ cần
    /// vẽ lại thứ nhận về chứ không tự cộng lấy.
    /// </para>
    /// <para>
    /// BR-PAY-004 — khách trả nửa tiền mặt nửa chuyển khoản thì gọi hai lần, mỗi lần một
    /// phương thức. Mỗi phương thức vốn đã là một dòng riêng nên không cần dạng danh sách.
    /// </para>
    /// </summary>
    [HttpPost("{id}/payments")]
    [RequireAuth]
    [RequirePermission(Feature.SalesInvoices, Write = true)]
    public async Task<IActionResult> RecordPayment(
        string id, [FromBody] RecordPaymentRequest? request, CancellationToken cancellationToken)
    {
        var invoice = await recordPayment.ExecuteAsync(
            new RecordPaymentCommand(id, request?.Method, request?.Amount ?? 0L, request?.Reference),
            Actor(),
            cancellationToken);

        return Created($"/api/sales-invoices/{invoice.Id}", new { invoice });
    }

    /// <summary>
    /// Hoàn tiền — BR-PAY-006, một dòng thu mang số <b>âm</b> kèm lý do bắt buộc.
    /// <para>
    /// Nhóm quyền là <c>Refunds</c> chứ không phải <c>SalesInvoices</c>: BR-PAY-007 cho lễ tân
    /// thu tiền cả ngày nhưng không cho họ trả tiền ra khỏi két, và ô ấy cố ý vắng mặt trong
    /// ma trận. Đây là endpoint <b>duy nhất</b> trên tài nguyên này mà lễ tân nhận 403.
    /// </para>
    /// <para>
    /// Là <c>POST</c> chứ không phải <c>PATCH .../status</c> với <c>REFUNDED</c>: hoàn tiền
    /// sinh ra một chứng từ mới — một dòng tiền có số tiền, phương thức và lý do — chứ không
    /// phải đổi một ô trạng thái. Trạng thái <c>REFUNDED</c> chỉ là hệ quả.
    /// </para>
    /// </summary>
    [HttpPost("{id}/refunds")]
    [RequireAuth]
    [RequirePermission(Feature.Refunds, Write = true)]
    public async Task<IActionResult> IssueRefund(
        string id, [FromBody] IssueRefundRequest? request, CancellationToken cancellationToken)
    {
        var invoice = await issueRefund.ExecuteAsync(
            new IssueRefundCommand(id, request?.Method, request?.Amount ?? 0L, request?.Reason),
            Actor(),
            cancellationToken);

        return Created($"/api/sales-invoices/{invoice.Id}", new { invoice });
    }

    private static IReadOnlyList<SalesInvoiceLineInput> ToLineInputs(
        IReadOnlyList<SalesInvoiceLineRequest>? lines)
        => [.. (lines ?? []).Select(line =>
            new SalesInvoiceLineInput(line.ServiceId, line.Name, line.UnitPrice, line.Quantity))];

    private ActorContext Actor()
        => requestScope.ToActor(HttpContext.Connection.RemoteIpAddress?.ToString());
}
