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
