using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.UseCases.Audit;
using NailManagement.Domain.Enums;

namespace NailManagement.API.Controllers;

/// <summary>
/// Đọc nhật ký kiểm toán — BR-AUD-005.
/// <para>
/// Chỉ có động từ GET. BR-AUD-004 quy định nhật ký không sửa và không xóa được, nên
/// controller này cố ý không có endpoint nào khác: quy tắc được diễn đạt bằng chính những
/// gì vắng mặt.
/// </para>
/// <para>
/// <c>RequiresTenant = false</c> vì Superadmin đọc toàn hệ thống mà tài khoản Superadmin
/// thì không thuộc tiệm nào. Với chủ tiệm, yêu cầu phải có tiệm đang làm việc được kiểm ở
/// use case — nơi biết rõ vai trò nào cần phạm vi nào.
/// </para>
/// </summary>
[ApiController]
[Route("api/audit-logs")]
public sealed class AuditLogsController(
    ListAuditLogsUseCase listAuditLogsUseCase,
    RequestScope requestScope) : ControllerBase
{
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.AuditLogs, RequiresTenant = false)]
    public async Task<IActionResult> List([FromQuery] int? take, CancellationToken cancellationToken)
    {
        var current = requestScope.Require();

        var entries = await listAuditLogsUseCase.ExecuteAsync(
            current.Role, current.ActiveTenantId, take, cancellationToken);

        return Ok(new { entries });
    }
}
