using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.Common;
using NailManagement.Application.Features.Staff;
using NailManagement.Application.Features.Staff.UseCases;
using NailManagement.Domain.Access;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.API.Controllers;

public sealed record SaveStaffRequest(
    string? BranchId,
    string? FullName,
    string? Phone,
    string? Email,
    string? Role,
    string? ShiftStart,
    string? ShiftEnd,
    decimal CommissionRate,
    IReadOnlyList<string>? Skills);

public sealed record ChangeStaffStatusRequest(string? Status);

/// <summary>Thân request cấp tài khoản đăng nhập cho một hồ sơ lễ tân — BR-AUTH-013.</summary>
public sealed record GrantStaffAccountRequest(
    string? Email,
    string? Username,
    string? DisplayName,
    string? Password);

/// <summary>
/// Hồ sơ nhân viên của tiệm đang làm việc.
/// <para>
/// Không endpoint nào nhận mã tiệm, và <b>cũng không nhận mã chi nhánh để lọc</b>. Chi
/// nhánh của lễ tân đến từ phiên đăng nhập qua <c>ActorContext.BranchId</c> (BR-EMP-004);
/// nhận nó từ client là để lễ tân tự khai mình thuộc chi nhánh nào, và ô "chỉ xem chi nhánh
/// mình" trong ma trận mục 3.4 sẽ chỉ còn là một quy ước của giao diện.
/// </para>
/// <para>
/// Lệnh cấp tài khoản gắn vào nhóm chức năng <c>ReceptionistAccounts</c> chứ không phải
/// <c>Staff</c>: theo ma trận, lễ tân được <i>xem</i> nhân viên nhưng không có ô nào ở hàng
/// tài khoản lễ tân. Gắn nhầm nhóm là cho lễ tân tự cấp tài khoản cho đồng nghiệp.
/// </para>
/// </summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffController(
    ListStaffUseCase listStaff,
    CreateStaffUseCase createStaff,
    UpdateStaffUseCase updateStaff,
    ChangeStaffStatusUseCase changeStaffStatus,
    GrantStaffAccountUseCase grantStaffAccount,
    RequestScope requestScope) : ControllerBase
{
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.Staff)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(new { staff = await listStaff.ExecuteAsync(Actor(), cancellationToken) });

    /// <summary>BR-EMP-008 — vượt <c>max_staff</c> thì trả <c>LIMIT_EXCEEDED</c>.</summary>
    [HttpPost]
    [RequireAuth]
    [RequirePermission(Feature.Staff, Write = true)]
    public async Task<IActionResult> Create(
        [FromBody] SaveStaffRequest? request, CancellationToken cancellationToken)
    {
        var staff = await createStaff.ExecuteAsync(ToCreateCommand(request), cancellationToken);

        return Created($"/api/staff/{staff.Id}", new { staff });
    }

    [HttpPut("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.Staff, Write = true)]
    public async Task<IActionResult> Update(
        string id, [FromBody] SaveStaffRequest? request, CancellationToken cancellationToken)
    {
        var staff = await updateStaff.ExecuteAsync(
            new UpdateStaffCommand(
                id,
                request?.BranchId ?? string.Empty,
                request?.FullName ?? string.Empty,
                request?.Phone,
                request?.Email,
                request?.Role ?? string.Empty,
                request?.ShiftStart ?? string.Empty,
                request?.ShiftEnd ?? string.Empty,
                request?.CommissionRate ?? 0m,
                request?.Skills),
            cancellationToken);

        return Ok(new { staff });
    }

    /// <summary>
    /// BR-EMP-005/006 — đây là thứ mà giao diện gọi là "cho nghỉ việc".
    /// <para>
    /// Cố ý KHÔNG có động từ <c>DELETE</c>: hồ sơ nhân viên không xóa được (BR-DEL-001) vì
    /// tên của họ còn phải hiện đúng trong lịch hẹn và hóa đơn cũ.
    /// </para>
    /// </summary>
    [HttpPatch("{id}/status")]
    [RequireAuth]
    [RequirePermission(Feature.Staff, Write = true)]
    public async Task<IActionResult> ChangeStatus(
        string id, [FromBody] ChangeStaffStatusRequest? request, CancellationToken cancellationToken)
    {
        var staff = await changeStaffStatus.ExecuteAsync(
            new ChangeStaffStatusCommand(id, request?.Status ?? string.Empty),
            Actor(),
            cancellationToken);

        return Ok(new { staff });
    }

    /// <summary>
    /// BR-AUTH-013 — cấp tài khoản đăng nhập trên một hồ sơ lễ tân đã tồn tại.
    /// <para>
    /// Trả 201 kèm <c>generatedPassword</c> khi máy chủ vừa tự sinh mật khẩu. Đó là lần duy
    /// nhất chuỗi ấy rời khỏi máy chủ, nên màn hình phải hiện nó ngay — hệ thống không gửi
    /// email và cũng không có đường đọc lại.
    /// </para>
    /// </summary>
    [HttpPost("{id}/account")]
    [RequireAuth]
    [RequirePermission(Feature.ReceptionistAccounts, Write = true)]
    public async Task<IActionResult> GrantAccount(
        string id, [FromBody] GrantStaffAccountRequest? request, CancellationToken cancellationToken)
    {
        var result = await grantStaffAccount.ExecuteAsync(
            new GrantStaffAccountCommand(
                id,
                request?.Email,
                request?.Username,
                request?.DisplayName,
                request?.Password),
            Actor(),
            cancellationToken);

        return Created(
            $"/api/staff/{id}/account",
            new { staff = result.Staff, generatedPassword = result.GeneratedPassword });
    }

    private static CreateStaffCommand ToCreateCommand(SaveStaffRequest? request) => new(
        request?.BranchId ?? string.Empty,
        request?.FullName ?? string.Empty,
        request?.Phone,
        request?.Email,
        request?.Role ?? string.Empty,
        request?.ShiftStart ?? string.Empty,
        request?.ShiftEnd ?? string.Empty,
        request?.CommissionRate ?? 0m,
        request?.Skills);

    /// <summary>
    /// Người thực hiện, dựng từ phiên đăng nhập chứ không từ thân request — BR-AUD-003.
    /// Ở controller này nó còn mang theo chi nhánh, thứ quyết định lễ tân nhìn thấy ai.
    /// </summary>
    private ActorContext Actor()
        => requestScope.ToActor(HttpContext.Connection.RemoteIpAddress?.ToString());
}
