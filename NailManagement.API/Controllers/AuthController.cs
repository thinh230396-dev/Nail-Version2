using Microsoft.AspNetCore.Mvc;
using NailManagement.Application.DTOs.Auth;
using NailManagement.Application.UseCases.Auth;

namespace NailManagement.API.Controllers;

/// <summary>Thân request của <c>POST /api/auth/login</c>.</summary>
public sealed record LoginRequest(string? Identifier, string? Password, bool Remember);

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
    GetCurrentAccountUseCase getCurrentAccountUseCase,
    LogoutUseCase logoutUseCase) : ControllerBase
{
    public const string SessionCookieName = "salonsys_session";

    [HttpPost("login")]
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

        return Ok(new { account = result.Account });
    }

    [HttpGet("session")]
    public async Task<IActionResult> Session(CancellationToken cancellationToken)
    {
        var result = await getCurrentAccountUseCase.ExecuteAsync(
            Request.Cookies[SessionCookieName],
            cancellationToken);

        return Ok(new { account = result.Account, activeTenantId = result.ActiveTenantId });
    }

    [HttpPost("logout")]
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
