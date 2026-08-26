using System.Text.Json;
using NailManagement.Application.Abstractions;
using NailManagement.Domain.Entities.Auditing;
using NailManagement.Domain.Repositories;

namespace NailManagement.Infrastructure.Auditing;

/// <summary>
/// Bản cài đặt <see cref="IAuditLogger"/>: sinh mã định danh, lấy giờ máy chủ, chuyển dữ
/// liệu kèm theo sang JSON rồi ghi xuống bảng nhật ký.
/// <para>
/// Ba việc đó là chi tiết kỹ thuật nên chúng nằm ở tầng Infrastructure. Use case chỉ khai
/// báo "chuyện gì vừa xảy ra, với ai, trên đối tượng nào" — đúng phần thuộc về nghiệp vụ.
/// </para>
/// </summary>
public sealed class AuditLogger(IAuditLogRepository entries, IClock clock, IIdGenerator ids) : IAuditLogger
{
    public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var log = AuditLog.Record(
            ids.NewId(),
            entry.Event,
            clock.UtcNow,
            entry.ActorUserId,
            entry.ActorRole,
            entry.TenantId,
            entry.TargetType,
            entry.TargetId,
            entry.Ip,
            entry.Metadata is null ? "{}" : JsonSerializer.Serialize(entry.Metadata));

        await entries.AddAsync(log, cancellationToken);
    }
}
