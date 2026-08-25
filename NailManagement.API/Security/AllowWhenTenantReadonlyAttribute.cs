namespace NailManagement.API.Security;

/// <summary>
/// Miễn cho endpoint này khỏi lệnh chặn ghi khi tiệm hết hạn — BR-TENANT-011.
/// <para>
/// Danh sách được phép mang dấu này rất ngắn, và mỗi mục đều vì cùng một lý do: <b>không có
/// nó thì tiệm hết hạn bị khóa cứng, không còn đường tự thoát ra</b>.
/// </para>
/// <list type="bullet">
///   <item>Gửi và hủy yêu cầu nâng cấp hoặc gia hạn gói — BR-TENANT-011</item>
///   <item>Nộp chứng từ thanh toán cho hóa đơn đăng ký — BR-TENANT-011</item>
///   <item>Đăng xuất — chặn cả lối ra thì người dùng mắc kẹt trong phiên</item>
///   <item>Đổi tiệm đang làm việc — chủ tiệm phải chuyển sang tiệm khác được</item>
/// </list>
/// <para>
/// Là một thuộc tính riêng chứ không phải một cờ trên <see cref="RequirePermissionAttribute"/>,
/// vì hai endpoint cuối trong danh sách trên là thao tác trên PHIÊN đăng nhập, không thuộc
/// nhóm chức năng nghiệp vụ nào để mà kiểm tra quyền.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AllowWhenTenantReadonlyAttribute : Attribute;
