using System.Text.Json;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Audit;
using NailManagement.Domain.Access;
using NailManagement.Domain.Auditing;
using NailManagement.Domain.Auth;

namespace NailManagement.Application.Features.Audit.UseCases;

/// <summary>
/// Đọc nhật ký kiểm toán — BR-AUD-005.
/// <para>
/// Phạm vi đọc do <b>vai trò</b> quyết định, không do tham số client gửi lên: Superadmin
/// đọc toàn hệ thống, chủ tiệm chỉ đọc tiệm đang làm việc, lễ tân không đọc được. Để client
/// tự khai phạm vi là mở đường cho một chủ tiệm gõ mã tiệm khác vào và đọc nhật ký của họ.
/// </para>
/// <para>
/// Bảng nhật ký là bảng duy nhất không mang bộ lọc tự động theo tiệm — vì Superadmin phải
/// đọc được toàn bộ — nên đây cũng là chỗ duy nhất phép lọc được viết tay. Đó là lý do use
/// case này kiểm tra vai trò một lần nữa dù bộ lọc quyền ở tầng API đã kiểm rồi: mất một
/// dòng lệnh, đổi lấy việc bảng dễ lộ nhất có hai lớp canh.
/// </para>
/// </summary>
public sealed class ListAuditLogsUseCase(IAuditLogRepository entries, IUserRepository users)
{
    private const int DefaultTake = 100;
    private const int MaxTake = 300;

    public async Task<IReadOnlyList<AuditLogDto>> ExecuteAsync(
        UserRole role,
        string? activeTenantId,
        int? take,
        CancellationToken cancellationToken = default)
    {
        var scope = role switch
        {
            UserRole.SuperAdmin => null,
            UserRole.TenantAdmin => activeTenantId ?? throw new TenantNotSelectedException(),
            _ => throw new ForbiddenException("Vai trò này không xem được nhật ký kiểm toán.")
        };

        var limit = Math.Clamp(take ?? DefaultTake, 1, MaxTake);
        var found = await entries.ListAsync(scope, limit, cancellationToken);

        // Dịch mã người thao tác sang tên, MỘT lượt cho cả trang. Bảng nhật ký cố ý chỉ lưu mã
        // (BR-AUD-003) vì tên đổi được còn mã thì không — nhưng "USR-SUPERADMIN" thì không ai
        // đọc được, nên phép dịch phải xảy ra ở đâu đó, và đây là chỗ rẻ nhất.
        //
        // `Distinct()` là phần đáng giá: một trang nhật ký thường do vài người tạo ra, nên ba
        // trăm dòng thường chỉ hỏi tới dăm bảy mã.
        var actorIds = found
            .Select(entry => entry.ActorUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToArray();

        var actors = await users.ListByIdsAsync(actorIds, cancellationToken);

        return
        [
            .. found.Select(entry => AuditLogMapper.ToDto(
                entry,
                entry.ActorUserId is not null && actors.TryGetValue(entry.ActorUserId, out var actor)
                    ? actor.DisplayName
                    : null))
        ];
    }
}
