using System.Security.Cryptography;
using NailManagement.Application.Abstractions;

namespace NailManagement.Infrastructure.Security;

/// <summary>
/// Sinh mật khẩu tạm bằng nguồn ngẫu nhiên mã hóa của hệ điều hành.
/// <para>
/// Dùng <see cref="RandomNumberGenerator"/> chứ không phải <c>Random</c>: <c>Random</c> chạy
/// từ một hạt giống đoán được, và mật khẩu đoán được thì cũng bằng không có mật khẩu. Đây
/// là cùng lý do khiến id phiên đăng nhập phải lấy từ GUID phiên bản 4.
/// </para>
/// <para>
/// Bộ ký tự cố ý bỏ những ký tự dễ đọc nhầm — chữ O và số 0, chữ l và số 1 — vì mật khẩu này
/// tồn tại để một người đọc trên màn hình rồi đọc lại cho người khác. Nhầm một ký tự là một
/// lần đăng nhập hỏng mà không ai hiểu vì sao.
/// </para>
/// <para>
/// Độ dài 14 ký tự trên bộ 58 ký tự cho khoảng 82 bit ngẫu nhiên, thừa sức cho một mật khẩu
/// chỉ dùng tới lần đăng nhập đầu tiên.
/// </para>
/// </summary>
public sealed class RandomPasswordGenerator : IPasswordGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
    private const string Symbols = "@#$%!";
    private const int Length = 14;

    public string Generate()
    {
        var characters = new char[Length];

        for (var index = 0; index < Length; index++)
        {
            // Một ký tự đặc biệt ở giữa để mật khẩu thỏa được cả những chính sách khắt khe
            // hơn nếu sau này có; phần còn lại là chữ và số cho dễ đọc lại.
            var source = index == Length / 2 ? Symbols : Alphabet;
            characters[index] = source[RandomNumberGenerator.GetInt32(source.Length)];
        }

        return new string(characters);
    }
}
