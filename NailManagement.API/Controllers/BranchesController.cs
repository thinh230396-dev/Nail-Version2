using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.DTOs;
using NailManagement.Application.UseCases.Branches;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.API.Controllers;

public sealed record SaveBranchRequest(
    string? Name,
    string? Code,
    string? Address,
    string? Phone);

public sealed record ChangeBranchStatusRequest(string? Status);

/// <summary>
/// Chi nhánh của tiệm đang làm việc.
/// <para>
/// Không endpoint nào nhận mã tiệm, kể cả trong đường dẫn. Tiệm đến từ phiên đăng nhập
/// (BR-AUTH-024) và bộ lọc toàn cục áp nó vào mọi truy vấn (BR-ISO-002). Nhận mã tiệm từ
/// client là biến một lời khai của trình duyệt thành phạm vi dữ liệu.
/// </para>
/// <para>
/// Lễ tân đọc được nhưng không ghi được — ma trận mục 3.4. Sự khác biệt đó nằm gọn ở cờ
/// <c>Write</c> trên từng endpoint, không phải ở một nhánh <c>if</c> nào trong use case.
/// </para>
/// </summary>
[ApiController]
[Route("api/branches")]
public sealed class BranchesController(
    ListBranchesUseCase listBranches,
    CreateBranchUseCase createBranch,
    UpdateBranchUseCase updateBranch,
    ChangeBranchStatusUseCase changeBranchStatus) : ControllerBase
{
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.Branches)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(new { branches = await listBranches.ExecuteAsync(cancellationToken) });

    /// <summary>BR-BRANCH-005 — vượt <c>max_salons</c> thì trả <c>LIMIT_EXCEEDED</c>.</summary>
    [HttpPost]
    [RequireAuth]
    [RequirePermission(Feature.Branches, Write = true)]
    public async Task<IActionResult> Create(
        [FromBody] SaveBranchRequest? request, CancellationToken cancellationToken)
    {
        var branch = await createBranch.ExecuteAsync(
            new CreateBranchCommand(
                request?.Name ?? string.Empty,
                request?.Code,
                request?.Address,
                request?.Phone),
            cancellationToken);

        return Created($"/api/branches/{branch.Id}", new { branch });
    }

    [HttpPut("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.Branches, Write = true)]
    public async Task<IActionResult> Update(
        string id, [FromBody] SaveBranchRequest? request, CancellationToken cancellationToken)
    {
        var branch = await updateBranch.ExecuteAsync(
            new UpdateBranchCommand(
                id,
                request?.Name ?? string.Empty,
                request?.Code,
                request?.Address,
                request?.Phone),
            cancellationToken);

        return Ok(new { branch });
    }

    /// <summary>
    /// BR-BRANCH-004 — đây là thứ mà giao diện gọi là "xóa chi nhánh".
    /// <para>
    /// Cố ý KHÔNG có động từ <c>DELETE</c> trên tài nguyên này. Chi nhánh không xóa được
    /// (BR-DEL-001) và chi nhánh chính thì đến ngừng hoạt động cũng không được
    /// (BR-BRANCH-002); để lộ một endpoint <c>DELETE</c> ra là hứa một việc mà hệ thống
    /// không làm.
    /// </para>
    /// </summary>
    [HttpPatch("{id}/status")]
    [RequireAuth]
    [RequirePermission(Feature.Branches, Write = true)]
    public async Task<IActionResult> ChangeStatus(
        string id, [FromBody] ChangeBranchStatusRequest? request, CancellationToken cancellationToken)
    {
        var branch = await changeBranchStatus.ExecuteAsync(
            new ChangeBranchStatusCommand(id, request?.Status ?? string.Empty),
            cancellationToken);

        return Ok(new { branch });
    }
}
