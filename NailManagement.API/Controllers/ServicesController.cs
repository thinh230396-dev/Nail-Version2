using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.DTOs;
using NailManagement.Application.UseCases.Services;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.API.Controllers;

public sealed record SaveServiceRequest(
    string? Name,
    string? Category,
    long Price,
    int DurationMinutes,
    int BufferMinutes,
    string? Description);

public sealed record ChangeServiceStatusRequest(string? Status);

/// <summary>
/// Bảng giá dịch vụ của tiệm đang làm việc — BR-SVC-001.
/// <para>
/// Không endpoint nào nhận mã tiệm, và cũng không endpoint nào nhận mã chi nhánh: dịch vụ
/// thuộc tiệm và dùng chung cho mọi chi nhánh, nên một tham số chi nhánh ở đây sẽ gợi ý một
/// khả năng mà hệ thống không có.
/// </para>
/// <para>
/// Lễ tân đọc được nhưng không ghi được — ma trận mục 3.4. Sự khác biệt đó nằm gọn ở cờ
/// <c>Write</c> trên từng endpoint, không phải ở một nhánh <c>if</c> nào trong use case.
/// </para>
/// </summary>
[ApiController]
[Route("api/services")]
public sealed class ServicesController(
    ListServicesUseCase listServices,
    CreateServiceUseCase createService,
    UpdateServiceUseCase updateService,
    ChangeServiceStatusUseCase changeServiceStatus) : ControllerBase
{
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.Services)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(new { services = await listServices.ExecuteAsync(cancellationToken) });

    [HttpPost]
    [RequireAuth]
    [RequirePermission(Feature.Services, Write = true)]
    public async Task<IActionResult> Create(
        [FromBody] SaveServiceRequest? request, CancellationToken cancellationToken)
    {
        var service = await createService.ExecuteAsync(
            new CreateServiceCommand(
                request?.Name ?? string.Empty,
                request?.Category,
                request?.Price ?? 0,
                request?.DurationMinutes ?? 0,
                request?.BufferMinutes ?? 0,
                request?.Description),
            cancellationToken);

        return Created($"/api/services/{service.Id}", new { service });
    }

    /// <summary>
    /// Sửa dịch vụ, kể cả đổi giá — BR-SVC-006 bảo đảm hóa đơn đã lập không đổi theo.
    /// </summary>
    [HttpPut("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.Services, Write = true)]
    public async Task<IActionResult> Update(
        string id, [FromBody] SaveServiceRequest? request, CancellationToken cancellationToken)
    {
        var service = await updateService.ExecuteAsync(
            new UpdateServiceCommand(
                id,
                request?.Name ?? string.Empty,
                request?.Category,
                request?.Price ?? 0,
                request?.DurationMinutes ?? 0,
                request?.BufferMinutes ?? 0,
                request?.Description),
            cancellationToken);

        return Ok(new { service });
    }

    /// <summary>
    /// BR-SVC-005 — đây là thứ mà giao diện gọi là "ngừng dịch vụ".
    /// <para>
    /// Cố ý KHÔNG có động từ <c>DELETE</c>: dịch vụ không xóa được (BR-DEL-001) vì tên của
    /// nó còn phải hiện đúng trong hóa đơn cũ, nên để lộ một endpoint <c>DELETE</c> ra là
    /// hứa một việc mà hệ thống không làm.
    /// </para>
    /// </summary>
    [HttpPatch("{id}/status")]
    [RequireAuth]
    [RequirePermission(Feature.Services, Write = true)]
    public async Task<IActionResult> ChangeStatus(
        string id, [FromBody] ChangeServiceStatusRequest? request, CancellationToken cancellationToken)
    {
        var service = await changeServiceStatus.ExecuteAsync(
            new ChangeServiceStatusCommand(id, request?.Status ?? string.Empty),
            cancellationToken);

        return Ok(new { service });
    }
}
