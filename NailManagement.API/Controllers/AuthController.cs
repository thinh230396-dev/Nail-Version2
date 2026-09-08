using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    RequestScope requestScope,
    IWebHostEnvironment environment) : ControllerBase
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
    // Chỉ endpoint này bị giới hạn theo IP. Gắn cho cả controller là chặn nhầm cả lệnh đọc
    // phiên — thứ mà giao diện gọi ở mỗi lần tải trang — và một người dùng bình thường sẽ tự
    // khóa mình chỉ bằng cách bấm chuyển màn hình vài chục lần.
    [EnableRateLimiting(RateLimitPolicy.Login)]
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
            branch = current.Branch,
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
            // Secure BẬT ở mọi môi trường trừ Development.
            //
            // Bản trước ghi cứng `false` với lý do "frontend gọi qua proxy Vite trên HTTP" —
            // đúng cho máy đang code, nhưng nó đi thẳng vào bản triển khai và ở đó thì cookie
            // phiên đi được qua HTTP, tức bất kỳ ai nghe được đường truyền cũng chiếm được
            // phiên. Điều kiện nên là "đang chạy dev hay không", không phải một hằng số.
            //
            // Cả hai profile trong launchSettings.json đều đặt ASPNETCORE_ENVIRONMENT là
            // Development, kể cả profile dùng lúc trình bày, nên buổi demo không bị ảnh hưởng.
            // Ngược lại, chạy ở môi trường khác mà không có HTTPS thì đăng nhập sẽ hỏng — và đó
            // là hành vi đúng: nó bắt người triển khai dựng TLS thay vì lặng lẽ chạy không có.
            Secure = !environment.IsDevelopment(),
            Path = "/",
            MaxAge = TimeSpan.FromSeconds(maxAgeSeconds)
        });
    }
}
