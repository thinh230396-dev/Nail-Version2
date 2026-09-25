using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.Features.Customers;
using NailManagement.Application.Features.Customers.UseCases;
using NailManagement.Domain.Access;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.API.Controllers;

/// <summary>
/// Thân request của cả <c>POST</c> lẫn <c>PUT</c>.
/// <para>
/// Không có <c>tier</c>, <c>points</c>, <c>visits</c> hay <c>totalSpent</c>: bốn con số đó
/// đều suy ra lúc đọc (BR-CUS-007/009) hoặc không tồn tại (BR-CUS-008). Nhận chúng từ client
/// là mở đường cho một hồ sơ tự khai mình là VIP.
/// </para>
/// </summary>
public sealed record SaveCustomerRequest(
    string? Phone,
    string? FullName,
    string? Email,
    string? BirthDate,
    string? Note);

public sealed record ChangeCustomerStatusRequest(string? Status);

/// <summary>
/// Danh bạ khách của tiệm đang làm việc — BR-CUS-001.
/// <para>
/// Không endpoint nào nhận mã tiệm, và cũng không endpoint nào nhận mã chi nhánh: khách
/// thuộc tiệm và dùng chung cho mọi chi nhánh. Đây là điểm khác <c>StaffController</c> —
/// ở đó lễ tân bị thu hẹp về chi nhánh mình, còn ở đây họ thấy toàn bộ khách của tiệm, đúng
/// theo BR-ISO-004. Khách đã đến chi nhánh Quận 1 hôm qua vẫn phải tra được ở Quận 3 hôm nay.
/// </para>
/// <para>
/// Cả chủ tiệm lẫn lễ tân đều có ô <c>Full</c> ở nhóm <c>Customers</c> trong ma trận mục
/// 3.4: thêm và sửa hồ sơ khách là công việc hằng ngày ở quầy, không phải quyền quản trị.
/// Superadmin thì <b>không có ô nào</b> — BR-AUTH-030.
/// </para>
/// </summary>
[ApiController]
[Route("api/customers")]
public sealed class CustomersController(
    ListCustomersUseCase listCustomers,
    GetCustomerUseCase getCustomer,
    CreateCustomerUseCase createCustomer,
    UpdateCustomerUseCase updateCustomer,
    ChangeCustomerStatusUseCase changeCustomerStatus) : ControllerBase
{
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.Customers)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(new { customers = await listCustomers.ExecuteAsync(cancellationToken) });

    /// <summary>
    /// Hồ sơ khách kèm những lần ghé gần nhất, đọc từ hóa đơn đã trả đủ.
    /// </summary>
    [HttpGet("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.Customers)]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
        => Ok(await getCustomer.ExecuteAsync(id, cancellationToken));

    /// <summary>BR-CUS-003 — chỉ số điện thoại là bắt buộc.</summary>
    [HttpPost]
    [RequireAuth]
    [RequirePermission(Feature.Customers, Write = true)]
    public async Task<IActionResult> Create(
        [FromBody] SaveCustomerRequest? request, CancellationToken cancellationToken)
    {
        var customer = await createCustomer.ExecuteAsync(
            new CreateCustomerCommand(
                request?.Phone ?? string.Empty,
                request?.FullName,
                request?.Email,
                request?.BirthDate,
                request?.Note),
            cancellationToken);

        return Created($"/api/customers/{customer.Id}", new { customer });
    }

    [HttpPut("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.Customers, Write = true)]
    public async Task<IActionResult> Update(
        string id, [FromBody] SaveCustomerRequest? request, CancellationToken cancellationToken)
    {
        var customer = await updateCustomer.ExecuteAsync(
            new UpdateCustomerCommand(
                id,
                request?.Phone ?? string.Empty,
                request?.FullName,
                request?.Email,
                request?.BirthDate,
                request?.Note),
            cancellationToken);

        return Ok(new { customer });
    }

    /// <summary>
    /// BR-CUS-006 — đây là thứ mà giao diện gọi là xóa khách hàng.
    /// <para>
    /// Cố ý KHÔNG có động từ <c>DELETE</c>: hồ sơ khách không xóa được (BR-DEL-001) vì tên
    /// của họ còn phải hiện đúng trên hóa đơn cũ, nên để lộ một endpoint <c>DELETE</c> ra là
    /// hứa một việc mà hệ thống không làm.
    /// </para>
    /// </summary>
    [HttpPatch("{id}/status")]
    [RequireAuth]
    [RequirePermission(Feature.Customers, Write = true)]
    public async Task<IActionResult> ChangeStatus(
        string id, [FromBody] ChangeCustomerStatusRequest? request, CancellationToken cancellationToken)
    {
        var customer = await changeCustomerStatus.ExecuteAsync(
            new ChangeCustomerStatusCommand(id, request?.Status ?? string.Empty),
            cancellationToken);

        return Ok(new { customer });
    }
}
