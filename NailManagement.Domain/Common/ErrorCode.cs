namespace NailManagement.Domain.Common;

/// <summary>
/// Mã lỗi máy đọc được, dùng chung cho toàn hệ thống.
/// Đây là phần <c>code</c> trong contract lỗi đã chốt: <c>{ error: { code, message, fields } }</c>.
/// <para>
/// Frontend dựa vào <c>code</c> để quyết định cách hiển thị, không dựa vào <c>message</c>.
/// </para>
/// <para>
/// Mã ở đây cố ý KHÔNG mang thông tin HTTP. Việc ánh xạ sang HTTP status là việc của
/// tầng API (<c>ApiExceptionHandler</c>) — tầng trong không được biết gì về HTTP.
/// </para>
/// </summary>
public static class ErrorCode
{
    /// <summary>Dữ liệu đầu vào sai. Kèm <c>fields</c> để frontend gắn thông báo vào đúng ô nhập.</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    /// <summary>
    /// Sai tài khoản hoặc mật khẩu — tách riêng khỏi <see cref="Unauthenticated"/>.
    /// Nếu dùng chung một mã, frontend sẽ hiểu nhầm là phiên hết hạn và đá người dùng
    /// về màn đăng nhập ngay giữa lúc họ đang đăng nhập.
    /// </summary>
    public const string InvalidCredentials = "INVALID_CREDENTIALS";

    /// <summary>Tài khoản đang bị khóa tạm do đăng nhập sai nhiều lần.</summary>
    public const string AccountLocked = "ACCOUNT_LOCKED";

    /// <summary>Tài khoản SUSPENDED hoặc INACTIVE — BR-AUTH-021.</summary>
    public const string AccountNotActive = "ACCOUNT_NOT_ACTIVE";

    /// <summary>Chưa đăng nhập, hoặc phiên đã hết hạn / bị thu hồi. Frontend đưa về màn đăng nhập.</summary>
    public const string Unauthenticated = "UNAUTHENTICATED";

    /// <summary>Đã đăng nhập nhưng không đủ quyền. Frontend KHÔNG được đưa về màn đăng nhập.</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>
    /// Đã đăng nhập nhưng chưa chọn tiệm để làm việc — BR-AUTH-024, BR-AUTH-025.
    /// <para>
    /// Đi cùng HTTP 403 như <see cref="Forbidden"/>, nhưng là một mã riêng vì frontend phải xử
    /// lý khác hẳn: FORBIDDEN dừng lại và báo "không có quyền", còn mã này đưa người dùng tới
    /// màn CHỌN TIỆM — thứ họ chỉ cần bấm một cái là đi tiếp được. Gộp hai thứ lại thì chủ tiệm
    /// quản nhiều tiệm bị báo thiếu quyền cho một việc chẳng liên quan gì tới quyền.
    /// </para>
    /// <para>
    /// Giữ 403 chứ không đổi sang 409: mã HTTP không phải chỗ mang ngữ nghĩa này, và đổi nó sẽ
    /// kéo theo mọi phép kiểm thử đang khẳng định 403 ở mười lăm đường ném.
    /// </para>
    /// </summary>
    public const string TenantNotSelected = "TENANT_NOT_SELECTED";

    /// <summary>Không tìm thấy, hoặc bản ghi không thuộc tenant đang làm việc — BR-TENANT-013 bước 4.</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>Tenant hết hạn hoặc bị khóa, mọi thao tác ghi bị chặn — BR-TENANT-010.</summary>
    public const string TenantReadonly = "TENANT_READONLY";

    /// <summary>Vượt hạn mức gói: max_salons hoặc max_staff — BR-BRANCH-005, BR-EMP-008.</summary>
    public const string LimitExceeded = "LIMIT_EXCEEDED";

    /// <summary>Kỹ thuật viên đã có lịch hẹn chồng giờ — BR-APT-011.</summary>
    public const string SlotConflict = "SLOT_CONFLICT";

    /// <summary>
    /// Gọi quá dày từ cùng một địa chỉ IP — hiện chỉ áp cho lệnh đăng nhập.
    /// <para>
    /// Khác <see cref="AccountLocked"/> ở chỗ nó đếm theo <b>nguồn gọi</b> chứ không theo tài
    /// khoản, và đó là lý do phải có cả hai. Khóa theo tài khoản chặn người dò nhiều mật khẩu
    /// vào <i>một</i> tài khoản; mã này chặn người rải <i>một</i> mật khẩu phổ biến qua hàng
    /// loạt tài khoản khác nhau — kịch bản mà bộ đếm theo tài khoản không bao giờ nhìn thấy,
    /// vì mỗi tài khoản chỉ sai đúng một lần.
    /// </para>
    /// </summary>
    public const string TooManyRequests = "TOO_MANY_REQUESTS";

    /// <summary>Lỗi không lường trước. Không bao giờ lộ chi tiết kỹ thuật ra ngoài.</summary>
    public const string Internal = "INTERNAL";
}
