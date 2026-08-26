using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.UseCases.Accounts;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.API.Controllers;

/// <summary>
/// Tài khoản chủ tiệm — chỉ đọc, chỉ Superadmin.
/// <para>
/// Chỉ có động từ GET, và đó là phạm vi có chủ đích. Việc cấp tài khoản chủ tiệm đã nằm sẵn
/// trong giao dịch tạo tiệm ở <c>TenantsController</c> (BR-TENANT-004), nên endpoint tạo
/// tài khoản rời sẽ là đường thứ hai dẫn tới cùng một kết quả — và là đường không đi kèm
/// tiệm, tức tạo ra tài khoản chủ tiệm không quản tiệm nào.
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
    ListTenantAdminAccountsUseCase listTenantAdminAccounts) : ControllerBase
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
}
