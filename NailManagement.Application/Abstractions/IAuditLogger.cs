using NailManagement.Domain.Enums.Auditing;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.Application.Abstractions;

/// <summary>Một sự kiện cần ghi vào nhật ký kiểm toán — BR-AUD-003.</summary>
/// <param name="TargetType">Loại đối tượng bị tác động, ví dụ <c>Tenant</c>, <c>AppUser</c>, <c>SalesInvoice</c>.</param>
/// <param name="Metadata">
/// Chi tiết riêng của từng loại sự kiện. Mỗi loại cần một bộ trường khác nhau, nên để dạng
/// cặp khóa–giá trị thay vì cố nhét tất cả vào các cột cố định.
/// </param>
public sealed record AuditEntry(
    AuditEvent Event,
    string? ActorUserId = null,
    UserRole? ActorRole = null,
    string? TenantId = null,
    string? TargetType = null,
    string? TargetId = null,
    string? Ip = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// Cổng ghi nhật ký kiểm toán — BR-AUD-001, ghi ở MÁY CHỦ.
/// <para>
/// Frontend hiện có một hàm ghi nhật ký phía trình duyệt với tên người thao tác và địa chỉ
/// IP gắn cứng. Đó là bản ghi giả mạo: bất kỳ ai mở công cụ nhà phát triển cũng sửa được.
/// Cổng này là chỗ thay thế nó, và bản ghi chỉ đáng tin khi máy chủ là nơi khai sinh ra nó.
/// </para>
/// <para>
/// Là cổng chứ không phải lời gọi thẳng vào kho dữ liệu, để use case không phải tự sinh mã
/// định danh, tự lấy giờ, tự chuyển dữ liệu kèm theo sang JSON.
/// </para>
/// </summary>
public interface IAuditLogger
{
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
