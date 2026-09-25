using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Sessions;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Repositories.Auth;

namespace NailManagement.Application.Features.Sessions.UseCases;

/// <summary>
/// Danh sách phiên đăng nhập để quản trị — BR-AUTH-032.
/// <para>
/// Cùng hình thu hẹp với <c>ListAuditLogsUseCase</c>: Superadmin thấy tất cả, chủ tiệm thấy
/// người của tiệm mình, lễ tân không thấy gì. Giữ đúng một khuôn cho hai màn nằm cạnh nhau là
/// cố ý — hai cách thu hẹp khác nhau cho hai danh sách trên cùng một trang là chỗ để lọt lỗi.
/// </para>
/// </summary>
public sealed class ListSessionsUseCase(ISessionRepository sessions, IClock clock)
{
    private const int DefaultTake = 100;
    private const int MaxTake = 300;

    public async Task<IReadOnlyList<SessionDto>> ExecuteAsync(
        UserRole role,
        string? activeTenantId,
        string currentSessionId,
        int? take,
        CancellationToken cancellationToken = default)
    {
        var scope = role switch
        {
            UserRole.SuperAdmin => null,
            UserRole.TenantAdmin => activeTenantId ?? throw new TenantNotSelectedException(),
            _ => throw new ForbiddenException("Vai trò này không xem được danh sách phiên đăng nhập.")
        };

        var limit = Math.Clamp(take ?? DefaultTake, 1, MaxTake);
        var found = await sessions.ListAsync(scope, limit, cancellationToken);
        var now = clock.UtcNow;

        return [.. found.Select(session => SessionMapper.ToDto(session, now, currentSessionId))];
    }
}
