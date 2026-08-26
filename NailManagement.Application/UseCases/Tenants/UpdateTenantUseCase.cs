using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Enums.Auditing;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Tenants;

/// <summary>
/// Sửa hồ sơ tiệm — tên, địa chỉ, số điện thoại, email liên hệ.
/// <para>
/// Cố ý KHÔNG sửa được gói, hạn dùng hay trạng thái. Ba thứ đó có đường riêng
/// (<see cref="RenewTenantUseCase"/>, <see cref="ChangeTenantStatusUseCase"/>, và luồng hóa
/// đơn đăng ký ở BR-INV-033) vì mỗi thứ kéo theo hệ quả khác nhau: đổi gói phải chốt lại
/// giá theo BR-SUB-004, đổi hạn dùng phải ghi nhận thời điểm gia hạn. Gộp tất cả vào một
/// lệnh "sửa tiệm" là mở đường bỏ qua những hệ quả đó.
/// </para>
/// </summary>
public sealed class UpdateTenantUseCase(
    ITenantRepository tenants,
    TenantReadService reader,
    IAuditLogger audit,
    IClock clock)
{
    public async Task<TenantDetailDto> ExecuteAsync(
        UpdateTenantCommand command, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var tenant = await tenants.FindByIdAsync(command.TenantId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tiệm.");

        tenant.UpdateProfile(command.Name, command.Address, command.Phone, command.ContactEmail, now);
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
                new Dictionary<string, string> { ["name"] = tenant.Name }),
            cancellationToken);

        return await reader.DescribeAsync(tenant, now, cancellationToken);
    }
}
