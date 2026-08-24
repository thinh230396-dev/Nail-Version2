namespace NailManagement.Application.Abstractions;

/// <summary>Cổng sinh định danh. Tách ra để test bơm được giá trị cố định.</summary>
public interface IIdGenerator
{
    /// <summary>
    /// Định danh ngẫu nhiên không đoán được — dùng cho id phiên đăng nhập.
    /// Đoán được id phiên là chiếm được phiên, nên nguồn ngẫu nhiên phải là loại mã hóa.
    /// </summary>
    string NewId();

    /// <summary>Định danh có tiền tố cho dễ đọc khi tra cứu, ví dụ <c>USR-A1B2C3</c>.</summary>
    string NewId(string prefix);
}
