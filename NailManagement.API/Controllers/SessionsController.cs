using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.Features.Sessions.UseCases;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.API.Controllers;

/// <summary>
/// Quản trị phiên đăng nhập — BR-AUTH-032, BR-AUTH-033.
/// <para>
/// Tách khỏi <c>AuthController</c> dù cùng làm việc trên bảng <c>AppSessions</c>. Bên đó là
/// phiên <b>của chính người gọi</b>: đăng nhập, đọc phiên mình, đổi tiệm, đăng xuất — ai cũng
/// gọi được. Bên này là phiên <b>của người khác</b>, và nó đứng sau ô quyền
/// <see cref="Feature.Sessions"/>. Gộp chung thì một controller mang hai mức quyền, và chỗ đó
/// là nơi dễ gắn nhầm attribute nhất.
/// </para>
/// </summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(
    ListSessionsUseCase listSessionsUseCase,
    RevokeSessionUseCase revokeSessionUseCase,
    RequestScope requestScope) : ControllerBase
{
    /// <summary>
    /// <c>RequiresTenant = false</c> vì Superadmin không có tiệm đang làm việc. Việc chủ tiệm
    /// bắt buộc phải chọn tiệm do use case tự đòi — cùng lối với nhật ký kiểm toán.
    /// </summary>
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.Sessions, RequiresTenant = false)]
    public async Task<IActionResult> List([FromQuery] int? take, CancellationToken cancellationToken)
    {
        var current = requestScope.Require();

        var sessions = await listSessionsUseCase.ExecuteAsync(
            current.Role,
            current.ActiveTenantId,
            requestScope.SessionId ?? string.Empty,
            take,
            cancellationToken);

        return Ok(new { sessions });
    }

    [HttpPost("{id}/revoke")]
    [RequireAuth]
    [RequirePermission(Feature.Sessions, RequiresTenant = false)]
    public async Task<IActionResult> Revoke(string id, CancellationToken cancellationToken)
    {
        var current = requestScope.Require();

        var session = await revokeSessionUseCase.ExecuteAsync(
            requestScope.ToActor(HttpContext.Connection.RemoteIpAddress?.ToString()),
            current.ActiveTenantId,
            requestScope.SessionId ?? string.Empty,
            id,
            cancellationToken);

        return Ok(new { session });
    }
}
