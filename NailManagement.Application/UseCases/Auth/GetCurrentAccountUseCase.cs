using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs.Auth;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Auth;

/// <summary>
/// Đọc tài khoản của phiên hiện tại.
/// <para>
/// <b>BR-AUTH-022 nằm ở đây.</b> Trạng thái tài khoản được kiểm tra lại ở mỗi lần đọc phiên,
/// không chỉ lúc đăng nhập. Nhờ vậy tài khoản vừa bị chuyển sang Suspended mất quyền ngay ở
/// request kế tiếp, thay vì dùng tiếp tới khi phiên hết hạn.
/// </para>
/// <para>
/// Từ ngày 3, middleware xác thực sẽ gọi chính use case này, nên đừng nhân bản phép kiểm
/// tra ở chỗ khác.
/// </para>
/// </summary>
public sealed class GetCurrentAccountUseCase(
    IUserRepository users,
    ISessionRepository sessions,
    IClock clock)
{
    public async Task<CurrentAccountResult> ExecuteAsync(string? sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new UnauthenticatedException();

        var now = clock.UtcNow;

        var session = await sessions.FindByIdAsync(sessionId, cancellationToken);
        if (session is null || !session.IsValidAt(now))
            throw new UnauthenticatedException();

        var user = await users.FindByIdAsync(session.UserId, cancellationToken);
        if (user is null)
            throw new UnauthenticatedException();

        // BR-AUTH-021 + BR-AUTH-022 — tài khoản không còn Active thì phiên hết giá trị.
        if (!user.IsActive())
            throw new UnauthenticatedException("Tài khoản đã bị khóa hoặc vô hiệu hóa.");

        session.Touch(now);
        await sessions.UpdateAsync(session, cancellationToken);

        return new CurrentAccountResult(AccountMapper.ToDto(user), session.ActiveTenantId);
    }
}
