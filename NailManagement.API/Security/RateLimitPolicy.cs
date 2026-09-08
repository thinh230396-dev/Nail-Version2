namespace NailManagement.API.Security;

/// <summary>
/// Tên các chính sách giới hạn tần suất, khai báo một chỗ.
/// <para>
/// Tên chính sách là một chuỗi phải khớp nhau ở hai nơi cách xa nhau — chỗ đăng ký trong
/// <c>Program.cs</c> và thuộc tính <c>[EnableRateLimiting]</c> trên endpoint. Gõ lệch một ký
/// tự thì ASP.NET Core ném lỗi lúc chạy chứ không phải lúc biên dịch, và nó chỉ ném khi có
/// người gọi đúng endpoint ấy — tức là có thể lọt qua cả buổi thử tay.
/// </para>
/// </summary>
public static class RateLimitPolicy
{
    /// <summary>Đăng nhập — đếm theo địa chỉ IP. Xem chú thích ở chỗ đăng ký trong Program.cs.</summary>
    public const string Login = "login";
}
