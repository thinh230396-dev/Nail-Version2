using NailManagement.Domain.Shared;

namespace NailManagement.Application.Common.Exceptions;

// Các lớp dưới kế thừa thẳng AppException thay vì qua một lớp cơ sở trung gian tên
// "ApplicationException", vì cái tên đó đã thuộc về System.ApplicationException của .NET
// và trùng tên sẽ gây nhầm lẫn khi đọc.

/// <summary>
/// Sai tài khoản hoặc mật khẩu.
/// Thông điệp cố ý không nói rõ sai ở đâu — nói "email không tồn tại" là để lộ tài khoản
/// nào có thật trong hệ thống.
/// </summary>
public sealed class InvalidCredentialsException()
    : AppException(ErrorCode.InvalidCredentials, "Tài khoản hoặc mật khẩu không đúng.");

/// <summary>Khóa tạm sau khi nhập sai quá số lần cho phép.</summary>
public sealed class AccountLockedException(DateTimeOffset lockedUntil)
    : AppException(
        ErrorCode.AccountLocked,
        "Tài khoản đang tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau.")
{
    public DateTimeOffset LockedUntil { get; } = lockedUntil;
}

/// <summary>BR-AUTH-021 — tài khoản Suspended hoặc Inactive không đăng nhập được.</summary>
public sealed class AccountNotActiveException()
    : AppException(
        ErrorCode.AccountNotActive,
        "Tài khoản chưa được kích hoạt hoặc đang bị khóa. Liên hệ quản trị viên để được mở lại.");

/// <summary>Chưa đăng nhập, phiên hết hạn, hoặc phiên đã bị thu hồi.</summary>
public sealed class UnauthenticatedException(string? message = null)
    : AppException(
        ErrorCode.Unauthenticated,
        message ?? "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

/// <summary>Đã đăng nhập nhưng không đủ quyền cho thao tác này.</summary>
public sealed class ForbiddenException(string? message = null)
    : AppException(
        ErrorCode.Forbidden,
        message ?? "Bạn không có quyền thực hiện thao tác này.");

/// <summary>
/// BR-TENANT-010 — tiệm quá hạn hoặc bị khóa: xem được mọi thứ nhưng không ghi được gì.
/// <para>
/// Tách khỏi <see cref="ForbiddenException"/> vì dưới mắt người dùng đây là hai chuyện khác
/// hẳn nhau: một bên là "bạn không có quyền", bên kia là "tiệm cần gia hạn thì mới ghi tiếp
/// được". Frontend dựa vào mã lỗi để hiện lời mời gia hạn thay vì báo lỗi phân quyền.
/// </para>
/// <para>
/// Thông điệp mặc định cố ý KHÔNG nói tiệm bị khóa <i>vì</i> hết hạn. BR-TENANT-010 gộp hai
/// tình huống khác hẳn nhau vào cùng một chế độ chỉ đọc: quá hạn thanh toán, và bị Superadmin
/// khóa tay. Câu chữ chỉ nhắc tới gia hạn sẽ đẩy một tiệm đang bị khóa tay đi chuyển khoản,
/// rồi họ phát hiện số tiền đó không mở lại được gì.
/// </para>
/// </summary>
public sealed class TenantReadonlyException(string? message = null)
    : AppException(
        ErrorCode.TenantReadonly,
        message ?? "Tiệm đang ở chế độ chỉ xem nên mọi thay đổi tạm khóa. "
                 + "Kiểm tra hạn sử dụng và trạng thái tiệm để mở lại.");

/// <summary>
/// BR-SUB-007 — gói đăng ký hiện tại chưa mở tính năng này.
/// <para>
/// Dùng chung mã lỗi FORBIDDEN với thiếu quyền vai trò, nhưng thông điệp khác hẳn: người
/// dùng cần biết đây là chuyện nâng gói, không phải chuyện xin cấp quyền.
/// </para>
/// </summary>
public sealed class FeatureLockedException(string capability)
    : AppException(
        ErrorCode.Forbidden,
        "Gói dịch vụ hiện tại chưa mở tính năng này. Nâng cấp gói để sử dụng.")
{
    public string Capability { get; } = capability;
}

/// <summary>
/// Tài khoản chủ tiệm đã đăng nhập nhưng chưa chọn tiệm để làm việc.
/// <para>
/// Đây KHÔNG phải lỗi: BR-AUTH-024 quy định tiệm đang làm việc nằm trong phiên, và tài
/// khoản quản nhiều tiệm thì luôn phải qua màn chọn tiệm. Mã lỗi riêng để frontend biết
/// đưa người dùng tới màn chọn tiệm thay vì đá về màn đăng nhập.
/// </para>
/// </summary>
public sealed class TenantNotSelectedException()
    : AppException(
        ErrorCode.TenantNotSelected,
        "Chưa chọn tiệm để làm việc. Vui lòng chọn tiệm rồi thử lại.");
