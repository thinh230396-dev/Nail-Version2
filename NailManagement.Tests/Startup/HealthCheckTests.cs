using System.Net;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Startup;

/// <summary>
/// Hai mức kiểm tra sức khỏe mà bộ cân bằng tải gọi — không đăng nhập, không mang cookie.
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class HealthCheckTests(SalonSysFactory factory)
{
    [Fact]
    public async Task Kiem_tra_song_tra_ve_ok_khong_can_dang_nhap()
    {
        using var anonymous = SalonSysClient.Anonymous(factory);

        var response = await anonymous.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("ok", response.Body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Kiem_tra_san_sang_bao_database_khoe()
    {
        using var anonymous = SalonSysClient.Anonymous(factory);

        var response = await anonymous.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Healthy", response.Body.GetProperty("status").GetString());

        var database = Assert.Single(response.Body.GetProperty("checks").EnumerateArray());
        Assert.Equal("database", database.GetProperty("name").GetString());
        Assert.Equal("Healthy", database.GetProperty("status").GetString());

        // Thân phản hồi không được mang chi tiết lỗi — xem HealthCheckResponse.
        Assert.False(database.TryGetProperty("exception", out _));
        Assert.False(database.TryGetProperty("description", out _));
    }
}
