using NailManagement.Domain.Common;

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
