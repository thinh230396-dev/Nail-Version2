namespace NailManagement.Application.DTOs.Auth;

/// <summary>Dữ liệu vào của <c>LoginUseCase</c>.</summary>
/// <param name="Identifier">Email hoặc username.</param>
/// <param name="Ip">Lấy từ request, ghi vào phiên để nhận ra phiên lạ.</param>
public sealed record LoginCommand(
    string Identifier,
    string Password,
    bool Remember,
    string? Ip,
    string? UserAgent);

/// <summary>
/// Phiên vừa cấp. Use case chỉ nói "phiên này sống bao lâu" — việc biến nó thành cookie
/// HttpOnly là chuyện của tầng API. Tầng Application không biết HTTP có cookie.
/// </summary>
public sealed record IssuedSessionDto(string Id, DateTimeOffset ExpiresAt, int MaxAgeSeconds);

public sealed record LoginResult(AccountDto Account, IssuedSessionDto Session);

/// <param name="ActiveTenantId">BR-AUTH-024 — ngày 1 luôn null, ngày 3 mới đặt được.</param>
public sealed record CurrentAccountResult(AccountDto Account, string? ActiveTenantId);
