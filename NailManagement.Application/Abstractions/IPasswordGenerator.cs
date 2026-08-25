namespace NailManagement.Application.Abstractions;

/// <summary>
/// Cổng sinh mật khẩu tạm cho tài khoản mới được cấp.
/// <para>
/// BR-AUTH-012 — không có chức năng tự đăng ký, mọi tài khoản đều do cấp trên tạo, nên
/// người tạo phải có một mật khẩu để bàn giao. Hệ thống MVP không gửi email (mục 9.4 của lộ
/// trình), vì vậy mật khẩu sinh ra được trả về đúng MỘT lần trong kết quả của lệnh tạo để
/// màn hình hiển thị cho Superadmin chép lại. Sau lần đó, database chỉ còn bản băm.
/// </para>
/// <para>
/// Là cổng chứ không phải một hàm tiện ích, để bài test bơm được giá trị cố định — nếu
/// không thì không có cách nào kiểm chứng tài khoản vừa tạo đăng nhập được.
/// </para>
/// </summary>
public interface IPasswordGenerator
{
    /// <summary>Mật khẩu đọc được, thỏa ràng buộc độ dài tối thiểu ở BR-VAL-001.</summary>
    string Generate();
}
