using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Startup;

/// <summary>
/// Những thứ một máy chủ chạy thật cần có quanh mọi request: header bảo mật, mã truy vết trong
/// thân lỗi, và IP thật của người dùng khi đứng sau reverse proxy.
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class HttpHardeningTests(SalonSysFactory factory)
{
    private const string ClientIp = "203.0.113.7";
    private const string ProxyIp = "10.0.0.5";
    private const string StrangerIp = "10.0.0.9";

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/khong-co-endpoint-nay")]
    public async Task Moi_phan_hoi_deu_mang_header_bao_mat(string path)
    {
        using var http = factory.CreateClient();

        using var response = await http.GetAsync(path);

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
    }

    [Fact]
    public async Task Phan_hoi_loi_do_bo_xu_ly_loi_dung_van_giu_header_bao_mat()
    {
        // Đi qua ApiExceptionHandler — nơi gọi Response.Clear() — chứ không qua phần dự phòng
        // 404. Header đặt sớm sẽ mất ở đây; header đặt lúc OnStarting thì không.
        using var http = factory.CreateClient();

        using var response = await http.GetAsync("/api/appointments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
    }

    [Fact]
    public async Task Than_loi_mang_ma_truy_vet()
    {
        using var anonymous = SalonSysClient.Anonymous(factory);

        var notFound = await anonymous.GetAsync("/api/khong-co-endpoint-nay");
        var unauthenticated = await anonymous.GetAsync("/api/appointments");

        Assert.False(string.IsNullOrWhiteSpace(TraceId(notFound)));
        Assert.False(string.IsNullOrWhiteSpace(TraceId(unauthenticated)));
        Assert.NotEqual(TraceId(notFound), TraceId(unauthenticated));
    }

    [Fact]
    public async Task Sau_proxy_tin_cay_phien_ghi_ip_that_cua_nguoi_dung()
    {
        using var behindProxy = new BehindProxyFactory(connectionIp: ProxyIp, trustedProxy: ProxyIp);

        Assert.Equal(ClientIp, await CurrentSessionIpAsync(behindProxy, forwardedFor: ClientIp));
    }

    [Fact]
    public async Task Nguon_la_tu_xung_ip_khac_thi_khong_duoc_tin()
    {
        // Kẻ dò mật khẩu gửi X-Forwarded-For tùy ý để mỗi lần thử mang một IP mới. Máy chủ chỉ
        // tin header ấy từ proxy đã khai, nên ở đây nó giữ nguyên IP kết nối.
        using var behindProxy = new BehindProxyFactory(connectionIp: StrangerIp, trustedProxy: ProxyIp);

        Assert.Equal(StrangerIp, await CurrentSessionIpAsync(behindProxy, forwardedFor: ClientIp));
    }

    private static async Task<string?> CurrentSessionIpAsync(SalonSysFactory server, string forwardedFor)
    {
        var http = server.CreateDefaultClient(new CookieContainerHandler());
        http.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);

        using var client = new SalonSysClient(http);
        await client.LoginAsync(SalonSysClient.SuperAdminEmail, SalonSysClient.SuperAdminPassword);

        var sessions = await client.GetAsync("/api/sessions");
        var current = sessions.Body.GetProperty("sessions").EnumerateArray()
            .Single(session => session.GetProperty("isCurrent").GetBoolean());

        return current.GetProperty("ip").GetString();
    }

    private static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.SingleOrDefault() : null;

    private static string? TraceId(ApiResponse response)
        => response.Body.GetProperty("error").TryGetProperty("traceId", out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
