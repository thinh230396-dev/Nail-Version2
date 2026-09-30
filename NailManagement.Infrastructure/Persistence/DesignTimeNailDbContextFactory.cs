using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NailManagement.Application.Abstractions;

namespace NailManagement.Infrastructure.Persistence;

/// <summary>
/// Cách công cụ EF (<c>dotnet ef</c>, migration bundle) dựng <see cref="NailDbContext"/> mà
/// <b>không</b> chạy máy chủ web.
/// <para>
/// Không có lớp này, công cụ dựng cả host của <c>Program.cs</c> để lấy DbContext — kéo theo
/// appsettings, rate limiter, seed… Migration bundle chạy trong ảnh <c>migrator</c> không có
/// appsettings nào bên cạnh, nên nó hỏng ngay ở bước dựng DbContext, trước cả khi đọc tới tham
/// số <c>--connection</c>.
/// </para>
/// <para>
/// Chuỗi kết nối ở đây chỉ để dựng được DbContext; lúc áp migration thật, <c>--connection</c>
/// của bundle ghi đè nó. Phạm vi tiệm để trống: công cụ chỉ làm việc với lược đồ, không đọc dữ
/// liệu của tiệm nào.
/// </para>
/// </summary>
public sealed class DesignTimeNailDbContextFactory : IDesignTimeDbContextFactory<NailDbContext>
{
    private const string LocalDefault =
        "Server=localhost;Database=NailManagement;Trusted_Connection=True;TrustServerCertificate=True";

    public NailDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default") is { Length: > 0 } configured
            ? configured
            : LocalDefault;

        var options = new DbContextOptionsBuilder<NailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new NailDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public string? ActiveTenantId => null;
    }
}
