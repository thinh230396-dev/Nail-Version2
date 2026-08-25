using NailManagement.Domain.Common;

namespace NailManagement.Application.Common.Exceptions;

/// <summary>
/// Không tìm thấy bản ghi.
/// <para>
/// BR-TENANT-013 bước 4 — bản ghi <b>thuộc tiệm khác</b> cũng trả về đúng lỗi này, không
/// phải 403. Trả 403 là vô tình xác nhận "bản ghi đó có tồn tại, chỉ là bạn không được
/// xem", và ghép nhiều câu trả lời như vậy lại là đoán được dữ liệu của tiệm khác.
/// </para>
/// </summary>
public sealed class NotFoundException(string? message = null)
    : AppException(ErrorCode.NotFound, message ?? "Không tìm thấy dữ liệu yêu cầu.");

/// <summary>
/// BR-SUB-005 — vượt một trong hai hạn mức được cưỡng chế thật: số chi nhánh
/// (BR-BRANCH-005) hoặc số nhân viên (BR-EMP-008).
/// <para>
/// Tách khỏi lỗi phân quyền vì đây không phải chuyện "bạn không được phép", mà là chuyện
/// "gói hiện tại không đủ chỗ". Frontend dựa vào mã lỗi để mời nâng gói thay vì báo lỗi quyền.
/// </para>
/// </summary>
public sealed class LimitExceededException(string message)
    : AppException(ErrorCode.LimitExceeded, message);
