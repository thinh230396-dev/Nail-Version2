using NailManagement.Domain.ValueObjects;

namespace NailManagement.Application.Abstractions;

public sealed record HashedPassword(string Hash, string Salt);

/// <summary>
/// Cổng băm mật khẩu.
/// <para>
/// Là cổng của tầng Application chứ không phải Domain, vì "băm bằng thuật toán nào" là
/// quyết định kỹ thuật, không phải quy tắc nghiệp vụ. Đổi thuật toán chỉ cần thay bản cài
/// đặt ở tầng Infrastructure, không đụng tới use case.
/// </para>
/// </summary>
public interface IPasswordHasher
{
    HashedPassword Hash(RawPassword password);

    /// <summary>So sánh phải chống được tấn công đo thời gian.</summary>
    bool Verify(string password, HashedPassword hashed);

    /// <summary>
    /// Một cặp hash/salt <b>hợp lệ nhưng không thuộc về ai</b>, để so với nó tốn đúng bằng so
    /// với mật khẩu của một tài khoản thật.
    /// <para>
    /// Có mặt vì một lý do rất cụ thể. <c>LoginUseCase</c> cố ý trả cùng một thông điệp cho
    /// "không tìm thấy tài khoản" và "sai mật khẩu", để người ngoài không dò được email nào có
    /// thật trong hệ thống. Nhưng nếu nhánh không-tìm-thấy thoát ra ngay thì nó <b>không chạy</b>
    /// phép băm — mà phép băm ấy tốn hàng chục mili-giây vì PBKDF2 chạy 210.000 vòng. Chênh
    /// lệch đó đo được bằng một chiếc đồng hồ bấm giây: câu chữ thì kín, thời gian thì hở.
    /// </para>
    /// <para>
    /// So với cặp này luôn trả <c>false</c> — không ai biết mật khẩu gốc của nó — nên chỗ gọi
    /// bỏ kết quả đi. Thứ cần ở đây là <b>thời gian đã tiêu</b>, không phải câu trả lời.
    /// </para>
    /// </summary>
    HashedPassword Decoy { get; }
}
