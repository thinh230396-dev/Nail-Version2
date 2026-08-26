using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Enums.Auditing;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Tenants;

/// <summary>
/// Xóa một tiệm — BR-TENANT-020, và "xóa" ở đây là <b>xóa mềm</b>.
/// <para>
/// Không có lệnh xóa cứng nào trong toàn hệ thống (BR-DEL-001). Dữ liệu của tiệm ở lại
/// database nguyên vẹn; nó chỉ biến mất khỏi mọi danh sách nhờ bộ lọc <c>DeletedAt == null</c>
/// ở tầng lưu trữ.
/// </para>
/// <para>
/// Hai hệ quả kèm theo, cả hai đều là quy tắc chứ không phải chi tiết kỹ thuật:
/// </para>
/// <list type="bullet">
///   <item>
///     BR-TENANT-021 — gỡ liên kết ở <c>UserTenants</c>. Tài khoản chủ tiệm vẫn tồn tại và
///     vẫn đăng nhập được; họ chỉ bị chặn khi không còn tiệm nào. Xóa luôn tài khoản là làm
///     mất quyền của họ với những tiệm khác mà họ đang quản lý.
///   </item>
///   <item>
///     BR-TENANT-022 — hóa đơn đăng ký giữ nguyên và vẫn tính vào doanh thu nền tảng. Đó là
///     lý do bảng hóa đơn chép sẵn tên tiệm thành cột riêng thay vì đọc qua khóa ngoại.
///   </item>
/// </list>
/// </summary>
public sealed class DeleteTenantUseCase(
    ITenantRepository tenants,
    IUserTenantRepository userTenants,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    public async Task ExecuteAsync(
        string tenantId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var tenant = await tenants.FindByIdAsync(tenantId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tiệm.");

        var name = tenant.Name;

        await unitOfWork.ExecuteInTransactionAsync<object?>(async ct =>
        {
            tenant.SoftDelete(now);
            await tenants.UpdateAsync(tenant, ct);
            await userTenants.UnlinkAllAsync(tenant.Id, ct);

            return null;
        }, cancellationToken);

        await audit.RecordAsync(
            new AuditEntry(
                AuditEvent.TenantDeleted,
                actor.UserId,
                actor.Role,
                tenant.Id,
                nameof(Tenant),
                tenant.Id,
                actor.Ip,
                new Dictionary<string, string> { ["name"] = name }),
            cancellationToken);
    }
}
