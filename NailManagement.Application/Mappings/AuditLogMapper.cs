using System.Text.Json;
using NailManagement.Application.DTOs;
using NailManagement.Domain.Entities.Auditing;
using NailManagement.Domain.Enums.Auditing;

namespace NailManagement.Application.Mappings;

/// <summary>Chuyển bản ghi nhật ký sang DTO. Chỉ có một chiều — nhật ký không nhận dữ liệu từ ngoài vào.</summary>
public static class AuditLogMapper
{
    /// <summary>
    /// Tám mã sự kiện đúng như BR-AUD-002 liệt kê.
    /// <para>
    /// Viết tay từng dòng thay vì đổi tên enum sang chữ hoa bằng một phép biến đổi chuỗi:
    /// phép biến đổi đó cho ra <c>LOGINFAILED</c> chứ không phải <c>LOGIN_FAILED</c>, và sai
    /// một dấu gạch dưới là frontend không nhận ra sự kiện. Bảng tra thì sai được ở đúng
    /// một chỗ và sửa cũng ở đúng chỗ đó.
    /// </para>
    /// </summary>
    public static string ToWireFormat(AuditEvent auditEvent) => auditEvent switch
    {
        AuditEvent.Login => "LOGIN",
        AuditEvent.LoginFailed => "LOGIN_FAILED",
        AuditEvent.TenantCreated => "TENANT_CREATED",
        AuditEvent.TenantUpdated => "TENANT_UPDATED",
        AuditEvent.TenantDeleted => "TENANT_DELETED",
        AuditEvent.AccountCreated => "ACCOUNT_CREATED",
        AuditEvent.AccountLocked => "ACCOUNT_LOCKED",
        AuditEvent.PaymentReceived => "PAYMENT_RECEIVED",
        AuditEvent.RefundIssued => "REFUND_ISSUED",
        AuditEvent.PackageChanged => "PACKAGE_CHANGED",
        _ => throw new ArgumentOutOfRangeException(nameof(auditEvent), auditEvent, "Sự kiện không hợp lệ.")
    };

    public static AuditLogDto ToDto(AuditLog entry) => new(
        entry.Id,
        ToWireFormat(entry.Event),
        entry.ActorUserId,
        entry.ActorRole is null ? null : AccountMapper.ToWireFormat(entry.ActorRole.Value),
        entry.TenantId,
        entry.TargetType,
        entry.TargetId,
        entry.Ip,
        entry.CreatedAt,
        ReadMetadata(entry.MetadataJson));

    /// <summary>
    /// Dữ liệu kèm theo được lưu dạng JSON vì mỗi loại sự kiện cần một bộ trường khác nhau.
    /// JSON hỏng thì trả về rỗng: một dòng nhật ký thiếu phần chi tiết vẫn hơn là cả màn
    /// hình nhật ký không mở được.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ReadMetadata(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}
