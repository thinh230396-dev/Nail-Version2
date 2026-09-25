using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.ValueObjects;
using NailManagement.Infrastructure.Persistence;
using NailManagement.Infrastructure.Persistence.Seed;

namespace NailManagement.API.Startup;

/// <summary>
/// Việc phải làm với database ngay khi máy chủ mở: áp migration, rồi mở một lối đăng nhập đầu
/// tiên.
///
/// <para>
/// Tách khỏi <c>Program.cs</c> vì đây là chỗ duy nhất trong hệ thống mà một nhánh <c>if</c> quyết
/// định <b>tài khoản nào tồn tại trên máy chủ</b>. Nằm lẫn giữa phần khai báo middleware thì nó
/// đọc như một dòng dọn dẹp cuối tệp, và nó đã từng bị đọc như vậy thật.
/// </para>
/// </summary>
public static class DatabaseBootstrap
{
    /// <summary>Khóa cấu hình của tài khoản quản trị đầu tiên, dùng ngoài môi trường Development.</summary>
    public const string AdminEmailKey = "Bootstrap:AdminEmail";

    /// <inheritdoc cref="AdminEmailKey"/>
    public const string AdminPasswordKey = "Bootstrap:AdminPassword";

    /// <inheritdoc cref="AdminEmailKey"/>
    public const string AdminDisplayNameKey = "Bootstrap:AdminDisplayName";

    public static async Task BootstrapDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<NailDbContext>();
        await db.Database.MigrateAsync();

        if (DemoSeedPolicy.ShouldSeedDemoData(app.Environment, app.Configuration))
        {
            await SeedDemoAsync(app, scope.ServiceProvider);
            return;
        }

        await BootstrapAdminAsync(app, scope.ServiceProvider);
    }

    /// <summary>
    /// Ba tài khoản demo và toàn bộ dữ liệu nghiệp vụ mẫu, để lần chạy đầu trên máy sạch không
    /// cần thao tác tay.
    /// </summary>
    private static async Task SeedDemoAsync(WebApplication app, IServiceProvider services)
    {
        var accountSeeder = services.GetRequiredService<DemoAccountSeeder>();
        var seededAccounts = await accountSeeder.SeedAsync();

        if (seededAccounts > 0)
        {
            app.Logger.LogInformation("Đã nạp {Count} tài khoản demo.", seededAccounts);
        }

        // Dữ liệu nghiệp vụ phải nạp SAU tài khoản: bộ nạp này gắn tài khoản lễ tân có sẵn vào
        // hồ sơ nhân viên vừa tạo (BR-AUTH-013), nên nó cần ba tài khoản kia đã nằm trong database.
        var dataSeeder = services.GetRequiredService<DemoDataSeeder>();

        if (await dataSeeder.SeedAsync())
        {
            app.Logger.LogInformation(
                "Đã nạp dữ liệu mẫu: gói dịch vụ, tiệm, nhân viên, khách hàng, lịch hẹn và hóa đơn.");
        }
    }

    /// <summary>
    /// Lối đăng nhập đầu tiên cho môi trường không nạp demo, lấy từ cấu hình.
    ///
    /// <para>
    /// Thiếu cấu hình thì <b>không tạo gì cả</b> và ghi một dòng cảnh báo. Không tự bịa ra một
    /// mật khẩu mặc định: một mật khẩu mặc định nằm trong mã nguồn chính là thứ mà cả lần vá này
    /// đi bỏ đi.
    /// </para>
    /// </summary>
    private static async Task BootstrapAdminAsync(WebApplication app, IServiceProvider services)
    {
        var email = app.Configuration[AdminEmailKey];
        var password = app.Configuration[AdminPasswordKey];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            app.Logger.LogWarning(
                "Không nạp dữ liệu demo ở môi trường {Environment}, và cũng chưa cấu hình {EmailKey} "
                + "cùng {PasswordKey}. Nếu database đang trống thì chưa có tài khoản nào để đăng nhập.",
                app.Environment.EnvironmentName,
                AdminEmailKey,
                AdminPasswordKey);

            return;
        }

        var seeder = services.GetRequiredService<BootstrapAdminSeeder>();

        if (await seeder.SeedAsync(email, password, app.Configuration[AdminDisplayNameKey]))
        {
            app.Logger.LogInformation("Đã tạo tài khoản quản trị đầu tiên từ cấu hình: {Email}", email);
        }
    }
}
