using System.Security.Cryptography;
using NailManagement.Application.Abstractions;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Infrastructure.Security;

/// <summary>
/// Băm mật khẩu bằng PBKDF2-HMAC-SHA256 của <c>System.Security.Cryptography</c>.
/// <para>
/// <b>Khác với backend cũ, có chủ đích.</b> Backend cũ trên Cloudflare Worker dùng SHA-256
/// một vòng, vì WebCrypto ở đó không có hàm băm chậm. SHA-256 tính rất nhanh nên thuận lợi
/// cho việc dò mật khẩu hàng loạt.
/// </para>
/// <para>
/// PBKDF2 nằm sẵn trong .NET, không phải cài gói nào, và số vòng lặp lớn khiến mỗi lần thử
/// tốn thời gian đáng kể — đó chính là thứ chặn tấn công dò. 210.000 vòng là mức OWASP
/// khuyến nghị cho PBKDF2-HMAC-SHA256.
/// </para>
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    private const int KeyLengthBytes = 32;
    private const int SaltLengthBytes = 16;

    /// <summary>
    /// Cặp mồi, dựng một lần cho cả tiến trình.
    /// <para>
    /// Hằng số viết cứng chứ không sinh ngẫu nhiên lúc khởi động, và đó là chủ ý: nó phải là
    /// một cặp <b>hợp lệ về hình thức</b> để <see cref="Verify"/> đi trọn đường tính toán thay
    /// vì thoát sớm ở bước giải mã hex. Giá trị cụ thể không quan trọng — không ai đăng nhập
    /// bằng nó được, vì không tồn tại mật khẩu nào băm ra đúng chuỗi này ngoài xác suất bằng 0.
    /// </para>
    /// <para>
    /// Độ dài phải khớp <see cref="KeyLengthBytes"/> và <see cref="SaltLengthBytes"/>. Lệch độ
    /// dài thì <c>FixedTimeEquals</c> trả về sớm và phép mồi mất tác dụng — có một phép thử
    /// canh đúng điều này.
    /// </para>
    /// </summary>
    private static readonly HashedPassword DecoyCredentials = new(
        "3C1A5B7E9D2F4068A1B3C5D7E9F02143658799AABBCCDDEEFF00112233445566",
        "0F1E2D3C4B5A69788796A5B4C3D2E1F0");

    public HashedPassword Decoy => DecoyCredentials;

    public HashedPassword Hash(RawPassword password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
        var key = Derive(password.Value, salt);

        return new HashedPassword(
            Convert.ToHexString(key),
            Convert.ToHexString(salt));
    }

    public bool Verify(string password, HashedPassword hashed)
    {
        byte[] expected;
        byte[] salt;

        try
        {
            expected = Convert.FromHexString(hashed.Hash);
            salt = Convert.FromHexString(hashed.Salt);
        }
        catch (FormatException)
        {
            // Dữ liệu băm trong database hỏng — coi như không khớp, đừng để văng lỗi 500
            // và qua đó tiết lộ rằng tài khoản này có thật.
            return false;
        }

        if (expected.Length != KeyLengthBytes) return false;

        var actual = Derive(password, salt);

        // So sánh theo thời gian hằng số: dùng SequenceEqual sẽ dừng ngay ở byte đầu khác
        // nhau, và chênh lệch thời gian đó đủ để suy ra dần từng byte.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeyLengthBytes);
}
