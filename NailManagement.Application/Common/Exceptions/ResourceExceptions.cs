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

/// <summary>
/// BR-APT-011 — kỹ thuật viên đã có một lịch hẹn chồng lấn khoảng giờ vừa chọn.
/// <para>
/// Là phép chặn cứng ở tầng API, không phải một lời cảnh báo ở giao diện: đây là một trong
/// bốn thứ mà lộ trình đánh dấu tuyệt đối không cắt, và một tiệm nail xếp hai khách cho cùng
/// một người làm là hỏng buổi của cả hai.
/// </para>
/// <para>
/// Thông điệp phải nói đủ <b>ai đang giữ chỗ và giữ từ mấy giờ tới mấy giờ</b>, vì contract
/// lỗi chỉ có ba trường <c>code</c>, <c>message</c>, <c>fields</c> và không có chỗ cho một
/// đối tượng đính kèm. Mở rộng contract dùng chung của cả chín module chỉ để một endpoint gửi
/// thêm một mã định danh là cái giá đắt hơn hẳn thứ nhận lại; câu chữ đầy đủ đã đủ để lễ tân
/// biết phải xếp khách vào lúc nào.
/// </para>
/// <para>
/// Lỗi được gắn vào ô <c>startAt</c> để biểu mẫu bôi đúng ô giờ, chứ không nổi lên thành một
/// thông báo chung rồi để người dùng tự đoán ô nào sai.
/// </para>
/// </summary>
public sealed class SlotConflictException(string message)
    : AppException(ErrorCode.SlotConflict, message, [new FieldError("startAt", message)]);
