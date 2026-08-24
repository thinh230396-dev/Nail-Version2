namespace NailManagement.Domain.Policies;

/// <summary>
/// Chính sách đăng nhập. Đây là quy tắc nghiệp vụ nên nằm ở tầng Domain, không phải
/// <c>appsettings.json</c> — thay đổi những con số này là thay đổi cách hệ thống hành xử,
/// không phải cách nó được triển khai.
/// <para>
/// Giá trị lấy nguyên từ backend cũ (<c>scripts/sites-worker.js</c> của repo frontend)
/// để hành vi không đổi khi chuyển sang ASP.NET Core.
/// </para>
/// </summary>
public static class AuthPolicy
{
    /// <summary>Số lần nhập sai liên tiếp trước khi khóa tạm.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>Thời gian khóa tạm sau khi vượt số lần cho phép.</summary>
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    /// <summary>Thời hạn phiên thường.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    /// <summary>Thời hạn phiên khi người dùng chọn "ghi nhớ đăng nhập".</summary>
    public static readonly TimeSpan RememberLifetime = TimeSpan.FromDays(30);

    public static TimeSpan LifetimeFor(bool remember) => remember ? RememberLifetime : SessionLifetime;
}
