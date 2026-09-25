namespace NailManagement.Domain.Auditing;

/// <summary>
/// BR-AUD-002 — hệ thống chỉ ghi đúng những loại sự kiện này, không ghi mọi thao tác.
/// BR-AUD-004: bản ghi nhật ký không sửa được và không xóa được.
/// </summary>
public enum AuditEvent
{
    Login = 1,
    LoginFailed = 2,
    TenantCreated = 3,
    TenantUpdated = 4,
    TenantDeleted = 5,
    AccountCreated = 6,

    /// <summary>
    /// Hệ thống <b>tự</b> khóa tạm sau năm lần nhập sai mật khẩu — không có người nào bấm.
    /// Đừng dùng lại cho thao tác khóa tay: xem <see cref="AccountSuspended"/>.
    /// </summary>
    AccountLocked = 7,

    PaymentReceived = 8,
    RefundIssued = 9,
    PackageChanged = 10,

    /// <summary>
    /// Superadmin <b>chủ động</b> khóa một tài khoản chủ tiệm — BR-AUTH-020.
    /// <para>
    /// Tách khỏi <see cref="AccountLocked"/> dù nhìn trên màn hình hai dòng đều đọc là "tài
    /// khoản bị khóa". Một sổ bảo mật phải trả lời được câu "ai đã làm việc này": khóa tự động
    /// là hệ quả của việc ai đó gõ sai mật khẩu năm lần, còn khóa tay là một quyết định có
    /// người chịu trách nhiệm. Gộp chung thì mọi dòng khóa tay đều mang dáng vẻ của một sự cố
    /// kỹ thuật, và ngược lại.
    /// </para>
    /// </summary>
    AccountSuspended = 11,

    /// <summary>Mở khóa cho một tài khoản đang bị khóa tay — cặp đôi của <see cref="AccountSuspended"/>.</summary>
    AccountRestored = 12,

    /// <summary>
    /// Đóng một phiên đăng nhập đang mở từ màn Bảo mật — BR-AUTH-033.
    /// <para>
    /// Không gộp vào <see cref="AccountSuspended"/> dù cả hai đều là "đá một người ra ngoài":
    /// khóa tài khoản chặn người đó đăng nhập lại, còn thu hồi phiên chỉ đóng đúng một thiết bị
    /// và họ đăng nhập lại được ngay. Một sổ trộn hai thứ này lại sẽ khiến người đọc tưởng mọi
    /// dòng đều là biện pháp nặng.
    /// </para>
    /// </summary>
    SessionRevoked = 13
}
