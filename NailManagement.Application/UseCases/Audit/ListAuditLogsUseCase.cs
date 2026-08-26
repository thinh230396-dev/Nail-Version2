using System.Text.Json;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Audit;

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
public sealed class ListAuditLogsUseCase(IAuditLogRepository entries)
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

        return [.. found.Select(AuditLogMapper.ToDto)];
    }
}
