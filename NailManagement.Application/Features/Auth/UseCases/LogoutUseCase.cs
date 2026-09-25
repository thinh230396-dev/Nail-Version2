using NailManagement.Application.Abstractions;
using NailManagement.Domain.Auth;

namespace NailManagement.Application.Features.Auth.UseCases;

/// <summary>
/// Đăng xuất — thu hồi phiên bằng <c>RevokedAt</c>, không xóa bản ghi (BR-DEL-001).
/// <para>
/// Cố ý không báo lỗi khi phiên không tồn tại hoặc đã hết hạn: người dùng bấm đăng xuất thì
/// kết quả họ mong đợi là "đã đăng xuất", và đó cũng là kết quả thật.
/// </para>
/// </summary>
public sealed class LogoutUseCase(ISessionRepository sessions, IClock clock)
{
    public async Task ExecuteAsync(string? sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        var session = await sessions.FindByIdAsync(sessionId, cancellationToken);
        if (session is null || session.RevokedAt is not null) return;

        session.Revoke(clock.UtcNow);
        await sessions.UpdateAsync(session, cancellationToken);
    }
}
