using NailManagement.Application.Abstractions;
using NailManagement.Domain.Entities;
using NailManagement.Domain.Enums;
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
        string DisplayName,
        string? TenantId,
        string? TenantName,
        string? BranchCode,
        string? BranchName);

    private static readonly Seed[] Accounts =
    [
        new("USR-SUPERADMIN", "superadmin@salonsys.vn", "superadmin", "Super@2026",
            UserRole.SuperAdmin, "Superadmin", null, null, null, null),

        new("USR-TENANT-LUMIERE", "tenantadmin@lumierehair.vn", "nguyenvanboss", "Lumiere@2026",
            UserRole.TenantAdmin, "Nguyễn Văn Boss", "TEN-LUMIERE", "Nailé Studio", null, null),

        new("USR-RECEPTION-NAILE", "receptionist@nailestudio.vn", "receptionist", "Reception@2026",
            UserRole.Receptionist, "Lê Hoàng Nam", "TEN-LUMIERE", "Nailé Studio",
            "Q3", "Nailé Studio · Chi nhánh Quận 3")
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

            user.AssignLegacyScope(seed.TenantId, seed.TenantName, seed.BranchCode, seed.BranchName);

            await users.AddAsync(user, cancellationToken);
        }

        return Accounts.Length;
    }
}
