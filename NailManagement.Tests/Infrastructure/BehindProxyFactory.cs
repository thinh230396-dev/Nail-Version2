using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NailManagement.API.Startup;

namespace NailManagement.Tests.Infrastructure;

/// <summary>
/// Máy chủ nhận request <b>từ một địa chỉ cố định</b>, như khi đứng sau reverse proxy.
/// <para>
/// Máy chủ trong bộ nhớ không có IP kết nối nào, nên phép thử không tự nhiên chạm được tới
/// <c>ForwardedHeadersMiddleware</c>. Một <see cref="IStartupFilter"/> đặt IP giả vào kết nối
/// <b>trước</b> toàn bộ chuỗi middleware của <c>Program.cs</c> — đúng vị trí của một proxy thật.
/// </para>
/// <para>
/// Không xóa database, cùng lý do với <see cref="ThrottledLoginFactory"/>.
/// </para>
/// </summary>
/// <param name="connectionIp">IP mà máy chủ thấy ở tầng kết nối.</param>
/// <param name="trustedProxy">IP được khai trong <c>ReverseProxy:KnownProxies</c>.</param>
public sealed class BehindProxyFactory(string connectionIp, string trustedProxy) : SalonSysFactory(dropDatabase: false)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{ReverseProxySetup.KnownProxiesKey}:0"] = trustedProxy
            }));

        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new ConnectionIpFilter(IPAddress.Parse(connectionIp))));
    }

    private sealed class ConnectionIpFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app =>
            {
                app.Use((context, following) =>
                {
                    context.Connection.RemoteIpAddress = address;
                    return following(context);
                });

                next(app);
            };
    }
}
