using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NailManagement.API.Startup;

/// <summary>
/// Thân JSON của <c>/api/health/ready</c>.
/// <para>
/// Chỉ nói tên, trạng thái và thời gian của từng phép kiểm — <b>không</b> kèm thông báo ngoại
/// lệ. Endpoint này mở cho bộ cân bằng tải gọi không cần đăng nhập, và một câu lỗi của SQL
/// Server là đủ để lộ tên máy chủ database ra ngoài. Chi tiết lỗi nằm trong log máy chủ.
/// </para>
/// </summary>
public static class HealthCheckResponse
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
        => context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            durationMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = (int)entry.Value.Duration.TotalMilliseconds
            })
        });
}
