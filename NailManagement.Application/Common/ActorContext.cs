using NailManagement.Domain.Access;

namespace NailManagement.Application.Common;

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
/// <param name="BranchId">
/// Chi nhánh của người đang thao tác, đọc qua hồ sơ nhân viên của họ (BR-EMP-004). Rỗng với
/// Superadmin và chủ tiệm — hai vai trò đó không gắn hồ sơ nhân viên nên không thuộc chi
/// nhánh nào, và cũng không bị giới hạn theo chi nhánh.
/// <para>
/// Có mặt ở đây thay vì ở <c>ITenantContext</c> là quyết định 31 chốt ngày 26/08: cổng
/// <c>ITenantContext</c> là thứ tầng lưu trữ dùng để cách ly <b>tiệm</b> ở mọi truy vấn, và
/// nhét thêm một khái niệm phạm vi thứ hai vào đó sẽ khiến nó không còn nói về đúng một
/// việc. Phạm vi chi nhánh chỉ áp cho vài endpoint đọc, nên nó đi cùng người thao tác.
/// </para>
/// </param>
public sealed record ActorContext(string UserId, UserRole Role, string? Ip, string? BranchId = null);
