using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Enums.Auditing;
using NailManagement.Domain.Enums.Platform;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Tenants;

/// <summary>
/// Khóa hoặc mở khóa một tiệm — BR-TENANT-002.
/// <para>
/// Chỉ nhận đúng hai giá trị <c>ACTIVE</c> và <c>SUSPENDED</c>, vì đó là hai trạng thái duy
/// nhất mà database lưu. <c>TRIAL</c> và <c>OVERDUE</c> là kết quả tính lúc đọc từ
/// <c>expires_at</c> và <c>is_trial</c>; cho phép đặt tay hai giá trị đó sẽ tạo ra những
/// tiệm mang trạng thái mâu thuẫn với chính ngày hết hạn của mình.
/// </para>
/// <para>
/// Khóa tiệm KHÔNG chặn đăng nhập. BR-TENANT-010 giữ nguyên quyền xem toàn bộ dữ liệu và
/// chỉ chặn ghi — phần chặn đó do <c>TenantWriteGuardMiddleware</c> lo, không phải ở đây.
/// </para>
/// </summary>
public sealed class ChangeTenantStatusUseCase(
    ITenantRepository tenants,
    TenantReadService reader,
    IAuditLogger audit,
    IClock clock)
{
    public async Task<TenantDetailDto> ExecuteAsync(
        ChangeTenantStatusCommand command, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var status = (command.Status ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => TenantStatus.Active,
            "SUSPENDED" => TenantStatus.Suspended,
            _ => throw DomainException.ForField(
                "status", "Trạng thái tiệm chỉ nhận ACTIVE hoặc SUSPENDED.")
        };

        var tenant = await tenants.FindByIdAsync(command.TenantId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tiệm.");

        if (status == TenantStatus.Suspended) tenant.Suspend(now);
        else tenant.Activate(now);

        await tenants.UpdateAsync(tenant, cancellationToken);

        await audit.RecordAsync(
            new AuditEntry(
                AuditEvent.TenantUpdated,
                actor.UserId,
                actor.Role,
                tenant.Id,
                nameof(Tenant),
                tenant.Id,
                actor.Ip,
                new Dictionary<string, string> { ["action"] = "STATUS", ["status"] = status.ToString() }),
            cancellationToken);

        return await reader.DescribeAsync(tenant, now, cancellationToken);
    }
}
