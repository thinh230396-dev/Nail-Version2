using NailManagement.Domain.Enums.Auth;

namespace NailManagement.Application.DTOs;

/// <summary>
/// Ai đang thực hiện thao tác, và từ đâu.
/// <para>
/// Tách khỏi các lệnh nghiệp vụ vì nó không thuộc về nội dung việc cần làm: một lệnh tạo
/// tiệm vẫn là lệnh tạo tiệm dù ai bấm nút. Gộp chung sẽ khiến mỗi lệnh mang thêm ba trường
/// lặp lại, và tệ hơn là mời người viết controller nhận chúng từ thân request — trong khi
/// giá trị đúng chỉ có thể lấy từ phiên đăng nhập ở máy chủ.
/// </para>
/// <para>
/// Đây là dữ liệu vào của <c>IAuditLogger</c> theo BR-AUD-003: bản ghi nhật ký phải nói
/// được ai làm, vai trò gì, từ địa chỉ nào.
/// </para>
/// </summary>
public sealed record ActorContext(string UserId, UserRole Role, string? Ip);
