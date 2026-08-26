using NailManagement.Application.Abstractions;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Repositories;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp ba tài khoản demo, giữ nguyên email và mật khẩu của backend cũ
/// (<c>scripts/vite-local-auth.ts</c> trong repo frontend) để không phải sửa gì ở màn hình
/// đăng nhập.
/// </summary>
public sealed class DemoAccountSeeder(
    IUserRepository users,
    IPasswordHasher hasher,
    IClock clock)
{
    private sealed record Seed(
        string Id,
        string Email,
        string Username,
        string Password,
        UserRole Role,
        string DisplayName);

    // Phạm vi làm việc của ba tài khoản này KHÔNG nằm ở đây nữa. Từ ngày 4, tiệm quản lý
    // được nằm ở bảng UserTenants và chi nhánh nằm trên hồ sơ nhân viên — cả hai do
    // DemoDataSeeder dựng, vì lúc bộ nạp này chạy thì chưa có tiệm nào tồn tại.
    private static readonly Seed[] Accounts =
    [
        new("USR-SUPERADMIN", "superadmin@salonsys.vn", "superadmin", "Super@2026",
            UserRole.SuperAdmin, "Superadmin"),

        new("USR-TENANT-LUMIERE", "tenantadmin@lumierehair.vn", "nguyenvanboss", "Lumiere@2026",
            UserRole.TenantAdmin, "Nguyễn Văn Boss"),

        new("USR-RECEPTION-NAILE", "receptionist@nailestudio.vn", "receptionist", "Reception@2026",
            UserRole.Receptionist, "Lê Hoàng Nam")
    ];

    /// <summary>
    /// Chỉ nạp khi bảng còn trống.
    /// <para>
    /// Điều kiện này là cố ý: chạy lại máy chủ không được ghi đè mật khẩu mà người dùng đã
    /// đổi, và cũng không được mở lại tài khoản đã bị khóa.
    /// </para>
    /// </summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await users.CountAsync(cancellationToken) > 0) return 0;

        var now = clock.UtcNow;

        foreach (var seed in Accounts)
        {
            var hashed = hasher.Hash(RawPassword.Create(seed.Password));

            var user = AppUser.Create(
                seed.Id,
                Email.Create(seed.Email),
                seed.Username,
                hashed.Hash,
                hashed.Salt,
                seed.Role,
                seed.DisplayName,
                now);

            await users.AddAsync(user, cancellationToken);
        }

        return Accounts.Length;
    }
}
