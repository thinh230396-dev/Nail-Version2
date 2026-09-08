using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.DTOs.Auth;
using NailManagement.Application.UseCases.Accounts;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.API.Controllers;

/// <summary>
/// Tài khoản chủ tiệm — đọc danh sách và khóa/mở khóa, chỉ Superadmin.
/// <para>
/// <b>Vẫn không có động từ tạo</b>, và đó là phạm vi có chủ đích. Việc cấp tài khoản chủ tiệm
/// đã nằm sẵn trong giao dịch tạo tiệm ở <c>TenantsController</c> (BR-TENANT-004), nên endpoint
/// tạo tài khoản rời sẽ là đường thứ hai dẫn tới cùng một kết quả — và là đường không đi kèm
/// tiệm, tức tạo ra tài khoản chủ tiệm không quản tiệm nào.
/// </para>
/// <para>
/// Cũng không có động từ xóa. BR-DEL-001 không cho xóa cứng, còn vô hiệu vĩnh viễn thì đi theo
/// hồ sơ nhân viên ở màn nhân sự của chủ tiệm chứ không phải một nút ở đây.
/// </para>
/// <para>
/// <c>RequiresTenant = false</c> vì cùng lý do với <c>TenantsController</c>: tài khoản
/// Superadmin không thuộc tiệm nào. Ma trận quyền là thứ cách ly ở đây — chỉ Superadmin có ô
/// <c>TenantAdminAccounts</c> (BR-AUTH-010).
/// </para>
/// </summary>
[ApiController]
[Route("api/accounts")]
public sealed class AccountsController(
    ListTenantAdminAccountsUseCase listTenantAdminAccounts,
    ChangeAccountStatusUseCase changeAccountStatus,
    RequestScope requestScope) : ControllerBase
{
    /// <summary>
    /// Danh sách tài khoản chủ tiệm.
    /// <para>
    /// Có tham số <c>role</c> để đường dẫn đọc đúng ý định ở phía gọi, nhưng hiện chỉ nhận
    /// đúng một giá trị: <c>TENANT_ADMIN</c>. Tài khoản lễ tân cố ý không lấy được ở đây —
    /// chúng thuộc phạm vi từng tiệm và sẽ có endpoint riêng nằm sau bộ lọc theo tiệm, còn
    /// endpoint này thì Superadmin gọi được nên nó phải không bao giờ trả về dữ liệu trong
    /// tiệm (BR-AUTH-030).
    /// </para>
    /// </summary>
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.TenantAdminAccounts, RequiresTenant = false)]
    public async Task<IActionResult> List(
        [FromQuery] string? role, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(role)
            && !string.Equals(role, "TENANT_ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            // Trả rỗng thay vì trả tất cả. Người gọi hỏi một thứ mà endpoint này không phục
            // vụ; đưa cho họ danh sách chủ tiệm là trả lời một câu hỏi khác với câu đã hỏi.
            return Ok(new { accounts = Array.Empty<object>() });
        }

        return Ok(new { accounts = await listTenantAdminAccounts.ExecuteAsync(cancellationToken) });
    }

    /// <summary>
    /// Khóa tạm hoặc mở khóa một tài khoản chủ tiệm — BR-AUTH-020.
    /// <para>
    /// Cùng hình dạng với <c>PATCH /api/tenants/{id}/status</c>: một đường dẫn, một trường
    /// <c>status</c>, hai giá trị. Hai màn hình này nằm cạnh nhau trong cổng Superadmin và
    /// người dùng nghĩ về chúng như một cặp — "khóa tiệm" và "khóa người quản tiệm" — nên hai
    /// endpoint đọc lên giống nhau là điều đáng giữ.
    /// </para>
    /// <para>
    /// <c>Write = true</c> chứ không chỉ <c>RequireAuth</c>: ô <c>TenantAdminAccounts</c> có
    /// hai mức, và trước lát cắt này chưa endpoint nào dùng tới mức ghi của nó.
    /// </para>
    /// </summary>
    [HttpPatch("{id}/status")]
    [RequireAuth]
    [RequirePermission(Feature.TenantAdminAccounts, RequiresTenant = false, Write = true)]
    public async Task<IActionResult> ChangeStatus(
        string id, [FromBody] ChangeAccountStatusRequest? request, CancellationToken cancellationToken)
    {
        var account = await changeAccountStatus.ExecuteAsync(
            new ChangeAccountStatusCommand(id, request?.Status ?? string.Empty),
            requestScope.ToActor(HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return Ok(new { account });
    }
}

/// <summary>
/// Thân request của <c>PATCH /api/accounts/{id}/status</c>.
/// <para>
/// <c>Status</c> để nullable và mọi phép kiểm dồn xuống use case — cùng lối
/// <c>ChangeTenantStatusRequest</c> đã đặt. Ràng buộc ở đây thì ASP.NET từ chối trước bằng
/// thông điệp của bộ nạp JSON, thứ mà người dùng cuối không đọc được và frontend không gắn
/// được vào đúng ô nhập.
/// </para>
/// </summary>
public sealed record ChangeAccountStatusRequest(string? Status);
