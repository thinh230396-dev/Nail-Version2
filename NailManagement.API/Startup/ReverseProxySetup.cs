using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace NailManagement.API.Startup;

/// <summary>
/// Đọc IP thật của người dùng khi máy chủ đứng sau reverse proxy (nginx, IIS, Azure Front Door…).
/// <para>
/// Sau proxy, <c>RemoteIpAddress</c> là IP <b>của proxy</b>. Không xử lý thì giới hạn đăng nhập
/// theo IP gom mọi người dùng vào một ngăn — 30 lần đăng nhập mỗi 5 phút cho <i>cả hệ thống</i>
/// — và nhật ký kiểm toán, danh sách phiên ghi toàn một địa chỉ.
/// </para>
/// <para>
/// Chỉ tin <c>X-Forwarded-For</c> từ proxy được khai báo. Tin từ bất kỳ ai là cho kẻ dò mật khẩu
/// tự đặt một IP mới cho mỗi lần thử và đi vòng qua chính bộ giới hạn ấy. Mặc định chỉ tin
/// loopback — đúng cho proxy chạy cùng máy; proxy ở máy khác phải khai trong cấu hình.
/// </para>
/// </summary>
public static class ReverseProxySetup
{
    /// <summary>Danh sách IP của proxy tin cậy, ví dụ <c>["10.0.0.5"]</c>.</summary>
    public const string KnownProxiesKey = "ReverseProxy:KnownProxies";

    /// <summary>Dải mạng của proxy tin cậy theo CIDR, ví dụ <c>["10.0.0.0/24"]</c>.</summary>
    public const string KnownNetworksKey = "ReverseProxy:KnownNetworks";

    public static IServiceCollection AddReverseProxySupport(
        this IServiceCollection services, IConfiguration configuration)
        => services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Proto cùng với For: sau proxy kết thúc TLS, thiếu Proto thì máy chủ tưởng mọi
            // request là http, chuyển hướng HTTPS vòng tròn mãi.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (var proxy in configuration.GetSection(KnownProxiesKey).Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(proxy));

            foreach (var network in configuration.GetSection(KnownNetworksKey).Get<string[]>() ?? [])
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });
}
