using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.DTOs.Auth;
using NailManagement.Application.UseCases.Auth;

namespace NailManagement.API.Controllers;

/// <summary>Thân request của <c>POST /api/auth/login</c>.</summary>
public sealed record LoginRequest(string? Identifier, string? Password, bool Remember);

/// <summary>Thân request của <c>POST /api/auth/session/tenant</c> — BR-AUTH-025.</summary>
public sealed record SelectTenantRequest(string? TenantId);

/// <summary>
/// Chuyển request HTTP thành lệnh gọi use case, rồi chuyển kết quả thành response.
/// <para>
/// Controller cố ý mỏng: nó chỉ đọc dữ liệu ra khỏi hình dạng HTTP và quyết định cookie.
/// Không có quy tắc nghiệp vụ nào ở đây — toàn bộ nằm trong use case. Lỗi thì để ném lên
/// cho <c>ApiExceptionHandler</c> xử lý, không bắt tại chỗ.
/// </para>
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    LoginUseCase loginUseCase,
    LogoutUseCase logoutUseCase,
    ListMyTenantsUseCase listMyTenantsUseCase,
    SelectActiveTenantUseCase selectActiveTenantUseCase,
    RequestScope requestScope) : ControllerBase
{
    public const string SessionCookieName = "salonsys_session";

    /// <summary>
    /// Đăng nhập.
    /// <para>
    /// Có <c>AllowWhenTenantReadonly</c> vì một lý do bắt được lúc thử tay: người dùng còn
    /// cookie của một phiên đang trỏ vào tiệm hết hạn mà bấm đăng nhập lại sẽ bị chính lệnh
    /// chặn ghi từ chối, và họ mắc kẹt ở màn đăng nhập không hiểu vì sao. Đăng nhập không
    /// phải thao tác ghi dữ liệu của tiệm nên nó không thuộc phạm vi BR-TENANT-010.
    /// </para>
    /// </summary>
    [HttpPost("login")]
    [AllowWhenTenantReadonly]
    public async Task<IActionResult> Login([FromBody] LoginRequest? request, CancellationToken cancellationToken)
    {
        var result = await loginUseCase.ExecuteAsync(
            new LoginCommand(
                request?.Identifier ?? string.Empty,
                request?.Password ?? string.Empty,
                request?.Remember ?? false,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null),
            cancellationToken);

        SetSessionCookie(result.Session.Id, result.Session.MaxAgeSeconds);

        return Ok(new { account = result.Account, mustSelectTenant = result.MustSelectTenant });
    }

    /// <summary>
    /// Trạng thái phiên hiện tại.
    /// <para>
    /// Không tự đi đọc phiên nữa: <c>SessionMiddleware</c> đã làm việc đó ở mỗi request và
    /// đặt kết quả vào <see cref="RequestScope"/>. Gọi lại use case ở đây là chạy hai lần
    /// cùng một chuỗi truy vấn cho cùng một request.
    /// </para>
    /// </summary>
    [HttpGet("session")]
    [RequireAuth]
    public IActionResult Session()
    {
        var current = requestScope.Require();

        return Ok(new
        {
            account = current.Account,
            activeTenantId = current.ActiveTenantId,
            tenant = current.Tenant,
            mustSelectTenant = current.MustSelectTenant
        });
    }

    /// <summary>BR-AUTH-023 — danh sách tiệm của tài khoản, dữ liệu cho màn chọn tiệm.</summary>
    [HttpGet("my-tenants")]
    [RequireAuth]
    public async Task<IActionResult> MyTenants(CancellationToken cancellationToken)
    {
        var current = requestScope.Require();
        var tenants = await listMyTenantsUseCase.ExecuteAsync(current.Account.Id, cancellationToken);

        return Ok(new { tenants });
    }

    /// <summary>
    /// BR-AUTH-025 — đổi tiệm đang làm việc.
    /// <para>
    /// Cố ý KHÔNG gắn <c>RequirePermission</c>: đây không phải một thao tác nghiệp vụ trong
    /// tiệm mà là thao tác trên chính phiên đăng nhập. Hơn nữa nó phải gọi được cả khi tiệm
    /// đang chọn đã hết hạn — nếu không thì chủ tiệm bị kẹt lại ở một tiệm hết hạn và không
    /// chuyển sang tiệm khác được.
    /// </para>
    /// </summary>
    [HttpPost("session/tenant")]
    [RequireAuth]
    [AllowWhenTenantReadonly]
    public async Task<IActionResult> SelectTenant(
        [FromBody] SelectTenantRequest? request, CancellationToken cancellationToken)
    {
        var tenant = await selectActiveTenantUseCase.ExecuteAsync(
            new SelectTenantCommand(requestScope.SessionId, request?.TenantId ?? string.Empty),
            cancellationToken);

        return Ok(new { tenant });
    }

    /// <summary>
    /// Đăng xuất là thu hồi phiên, không xóa bản ghi (BR-DEL-001).
    /// <para>
    /// Có <c>AllowWhenTenantReadonly</c> vì lối ra phải luôn mở: tiệm hết hạn mà không đăng
    /// xuất được thì người dùng mắc kẹt trong phiên của chính mình.
    /// </para>
    /// </summary>
    [HttpPost("logout")]
    [AllowWhenTenantReadonly]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await logoutUseCase.ExecuteAsync(Request.Cookies[SessionCookieName], cancellationToken);

        // Max-Age = 0 để trình duyệt xóa cookie ngay.
        SetSessionCookie(string.Empty, 0);

        return NoContent();
    }

    private void SetSessionCookie(string value, int maxAgeSeconds)
    {
        Response.Cookies.Append(SessionCookieName, value, new CookieOptions
        {
            // HttpOnly: JavaScript không đọc được cookie, nên kịch bản XSS không lấy được phiên.
            HttpOnly = true,
            // Strict: trình duyệt không gửi cookie kèm request đến từ trang khác — chặn CSRF.
            SameSite = SameSiteMode.Strict,
            // Frontend gọi qua proxy của Vite trên HTTP ở máy cục bộ, nên Secure phải tắt khi
            // chạy dev, nếu không trình duyệt sẽ bỏ qua cookie.
            Secure = false,
            Path = "/",
            MaxAge = TimeSpan.FromSeconds(maxAgeSeconds)
        });
    }
}
