namespace NailManagement.Application.Abstractions;

/// <summary>
/// Cổng gom nhiều lệnh ghi vào <b>một giao dịch</b>: hoặc tất cả cùng thành công, hoặc
/// không có gì được ghi.
/// <para>
/// BR-TENANT-004/005 là lý do cổng này tồn tại. Tạo một tiệm gồm năm việc — tiệm, chi nhánh
/// chính, tài khoản chủ tiệm, dòng liên kết tài khoản với tiệm, và hóa đơn đăng ký. Hỏng ở
/// việc thứ tư mà bốn việc kia đã ghi xong thì hệ thống còn lại một tiệm không ai quản lý
/// được, và không màn hình nào cho phép sửa chữa tình trạng đó.
/// </para>
/// <para>
/// Cố ý KHÔNG mang hình dạng "theo dõi thay đổi rồi lưu một lần" như <c>DbContext</c> của
/// EF Core: các repository hiện tại tự gọi lệnh lưu ngay khi được gọi, và cổng này chỉ bọc
/// một ranh giới giao dịch quanh chúng. Nhờ vậy tầng Application không cần biết lệnh lưu
/// xảy ra lúc nào — đúng tinh thần của Clean Architecture, nơi tầng trong không được biết
/// tầng ngoài lưu dữ liệu bằng cách gì.
/// </para>
/// </summary>
public interface IUnitOfWork
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default);
}
