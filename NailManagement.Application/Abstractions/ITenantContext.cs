namespace NailManagement.Application.Abstractions;

/// <summary>
/// Cổng đọc tiệm mà phiên đăng nhập hiện tại đang làm việc — BR-AUTH-024.
/// <para>
/// Đây là mảnh ghép giữa phiên đăng nhập và tầng lưu trữ: tầng lưu trữ hỏi cổng này để tự
/// gắn điều kiện lọc theo tiệm vào MỌI truy vấn, đúng yêu cầu của BR-ISO-002 rằng phép lọc
/// phải nằm ở một chỗ duy nhất chứ không để từng endpoint tự viết.
/// </para>
/// <para>
/// Cố ý CHỈ có phần đọc. Nếu use case đổi được tiệm đang làm việc thì mọi hàng rào cách ly
/// tenant chỉ còn là quy ước — một dòng lệnh đặt lại giá trị này là mở toang dữ liệu của
/// tiệm khác. Việc đặt giá trị nằm ở tầng Infrastructure, do middleware phiên gọi sau khi
/// đã kiểm tra tài khoản có quyền với tiệm đó qua bảng <c>UserTenants</c> (BR-ISO-003).
/// </para>
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Mã tiệm đang làm việc, hoặc null khi request không thuộc phạm vi tiệm nào — chưa
    /// đăng nhập, hoặc là Superadmin.
    /// </summary>
    string? ActiveTenantId { get; }

    bool HasTenant => ActiveTenantId is not null;
}
