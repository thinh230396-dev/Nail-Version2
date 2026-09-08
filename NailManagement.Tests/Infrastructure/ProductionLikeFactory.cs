namespace NailManagement.Tests.Infrastructure;

/// <summary>
/// Máy chủ chạy ở môi trường <b>không phải Development</b>, để kiểm những thứ chỉ bật ở đó.
/// <para>
/// Hiện có đúng một khách hàng: phép thử cờ <c>Secure</c> của cookie phiên. Phép thử ấy ở môi
/// trường Development chỉ khẳng định được cờ đang TẮT — mà cờ tắt là đúng cả trước lẫn sau lần
/// vá, nên nó không chứng minh được gì về chính lần vá. Muốn chứng minh thì phải hỏi ở phía
/// bên kia của điều kiện.
/// </para>
/// <para>
/// Dùng <c>Staging</c> chứ không phải <c>Production</c>, và khác biệt là cố ý: cả hai đều làm
/// <c>IsDevelopment()</c> trả về false — đúng thứ cần — nhưng <c>Staging</c> nói rõ với người
/// đọc rằng đây là một môi trường dựng riêng cho phép thử, không phải một bản triển khai thật.
/// </para>
/// <para>
/// <b>Không xóa database</b>, cùng lý do với <see cref="ThrottledLoginFactory"/>: nó dùng lại
/// database mà factory dùng chung đã dựng.
/// </para>
/// </summary>
public sealed class ProductionLikeFactory : SalonSysFactory
{
    public ProductionLikeFactory() : base(dropDatabase: false)
    {
    }

    protected override string EnvironmentName => "Staging";
}
