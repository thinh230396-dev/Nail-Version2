using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Tenants;
using NailManagement.Domain.Auditing;
using NailManagement.Domain.Platform.Tenants;

namespace NailManagement.Application.Features.Tenants.UseCases;

/// <summary>
/// Gia hạn tiệm — BR-TENANT-006, Superadmin nhập tay ngày hết hạn mới.
/// <para>
/// Không có phép cộng tự động nào ở đây (thêm 30 ngày, thêm 1 năm). Ngày mới do người nhập
/// quyết định, vì thời hạn thật phụ thuộc vào lúc tiệm chuyển khoản chứ không phải lúc
/// Superadmin bấm nút.
/// </para>
/// <para>
/// Gia hạn cũng tắt cờ dùng thử: BR-SUB-011 quy định tiệm đã trả tiền thì không còn ở trạng
/// thái dùng thử nữa. Phép đó nằm trong <c>Tenant.Renew</c>, tức là ở entity — nó luôn đúng
/// bất kể ai gọi tới.
/// </para>
/// </summary>
public sealed class RenewTenantUseCase(
    ITenantRepository tenants,
    TenantReadService reader,
    IAuditLogger audit,
    IClock clock)
{
    public async Task<TenantDetailDto> ExecuteAsync(
        RenewTenantCommand command, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var tenant = await tenants.FindByIdAsync(command.TenantId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tiệm.");

        var previousExpiry = tenant.ExpiresAt;

        tenant.Renew(command.ExpiresAt, now);
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
                new Dictionary<string, string>
                {
                    ["action"] = "RENEW",
                    ["from"] = previousExpiry.ToString("O"),
                    ["to"] = tenant.ExpiresAt.ToString("O")
                }),
            cancellationToken);

        return await reader.DescribeAsync(tenant, now, cancellationToken);
    }
}
