using NailManagement.Domain.Access;
using NailManagement.Domain.Shared;

namespace NailManagement.Domain.Auditing;

/// <summary>
/// Nhật ký kiểm toán — BR-AUD-001, ghi ở SERVER chứ không ở trình duyệt.
/// <para>
/// Frontend hiện có một hàm ghi nhật ký phía client với tên người thao tác và địa chỉ IP
/// gắn cứng; đó là dữ liệu giả mạo và BR-AUD-001 yêu cầu bỏ hẳn. Bản ghi chỉ đáng tin khi
/// chính máy chủ là nơi khai sinh ra nó.
/// </para>
/// <para>
/// BR-AUD-004 — không sửa được, không xóa được: entity này cố ý CHỈ có hàm tạo, không có
/// một hàm thay đổi nào. Đó là cách diễn đạt quy tắc bằng chính kiểu dữ liệu.
/// </para>
/// </summary>
public class AuditLog
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private AuditLog()
    {
        Id = string.Empty;
        MetadataJson = "{}";
    }

    private AuditLog(
        string id,
        AuditEvent auditEvent,
        string? actorUserId,
        UserRole? actorRole,
        string? tenantId,
        string? targetType,
        string? targetId,
        string? ip,
        string metadataJson,
        DateTimeOffset now)
    {
        Id = id;
        Event = auditEvent;
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        TenantId = tenantId;
        TargetType = targetType;
        TargetId = targetId;
        Ip = ip;
        MetadataJson = metadataJson;
        CreatedAt = now;
    }

    public string Id { get; private set; }

    /// <summary>BR-AUD-002 — chỉ ghi tám nhóm sự kiện đã liệt kê, không ghi mọi thao tác.</summary>
    public AuditEvent Event { get; private set; }

    /// <summary>Rỗng khi sự kiện xảy ra trước lúc xác định được người dùng, ví dụ đăng nhập sai.</summary>
    public string? ActorUserId { get; private set; }

    public UserRole? ActorRole { get; private set; }

    /// <summary>
    /// BR-AUD-005 — chủ tiệm chỉ xem được nhật ký của tiệm mình, nên cột này là chỗ để lọc.
    /// Rỗng với các sự kiện ở tầng nền tảng mà Superadmin thực hiện.
    /// </summary>
    public string? TenantId { get; private set; }

    public string? TargetType { get; private set; }
    public string? TargetId { get; private set; }
    public string? Ip { get; private set; }

    /// <summary>Chi tiết kèm theo, dạng JSON — mỗi loại sự kiện cần một bộ trường khác nhau.</summary>
    public string MetadataJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static AuditLog Record(
        string id,
        AuditEvent auditEvent,
        DateTimeOffset now,
        string? actorUserId = null,
        UserRole? actorRole = null,
        string? tenantId = null,
        string? targetType = null,
        string? targetId = null,
        string? ip = null,
        string metadataJson = "{}")
        => new(
            Guard.Reference(id, "id", "Mã bản ghi nhật ký"),
            auditEvent,
            actorUserId,
            actorRole,
            tenantId,
            Guard.Optional(targetType, "targetType", "Loại đối tượng", 64),
            Guard.Optional(targetId, "targetId", "Mã đối tượng", 64),
            Guard.Optional(ip, "ip", "Địa chỉ IP", 64),
            metadataJson,
            now);
}
