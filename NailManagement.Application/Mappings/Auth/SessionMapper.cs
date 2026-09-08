using NailManagement.Application.DTOs.Auth;
using NailManagement.Domain.Entities.Auth;

namespace NailManagement.Application.Mappings.Auth;

public static class SessionMapper
{
    public static SessionDto ToDto(AppSession session, DateTimeOffset now, string currentSessionId)
    {
        var (browser, os) = ParseUserAgent(session.UserAgent);

        return new(
            session.Id,
            session.UserId,
            session.User?.DisplayName ?? session.UserId,
            session.User?.Email.Value ?? string.Empty,
            // Phải đi qua AccountMapper chứ không được ToString().ToUpperInvariant(): phép
            // ấy biến TenantAdmin thành "TENANTADMIN", trong khi frontend đọc "TENANT_ADMIN".
            // Hai vai kia trùng nhau một cách tình cờ nên lỗi này chỉ lộ ra ở đúng một vai.
            session.User is null ? string.Empty : AccountMapper.ToWireFormat(session.User.Role),
            session.ActiveTenantId,
            session.Ip,
            session.UserAgent,
            DescribeDevice(session.UserAgent, browser, os),
            browser,
            os,
            session.CreatedAt,
            session.LastActive,
            session.ExpiresAt,
            session.RevokedAt,
            DescribeStatus(session, now),
            session.Id == currentSessionId);
    }

    /// <summary>
    /// Ba trạng thái, và thứ tự kiểm là bắt buộc: <b>thu hồi thắng hết hạn</b>.
    /// <para>
    /// Một phiên bị thu hồi rồi để quá ngày sẽ đúng cả hai điều kiện. Nếu hỏi hết hạn trước
    /// thì màn hình báo "hết hạn" cho một phiên mà người quản trị đã chủ động đóng — xóa mất
    /// dấu vết của một thao tác có chủ ý, đúng thứ mà màn Bảo mật sinh ra để hiển thị.
    /// </para>
    /// </summary>
    private static string DescribeStatus(AppSession session, DateTimeOffset now)
        => session.RevokedAt is not null ? "REVOKED"
            : session.ExpiresAt <= now ? "EXPIRED"
            : "ACTIVE";

    /// <summary>
    /// Rút gọn <c>User-Agent</c> thành một câu đọc được.
    /// <para>
    /// Cố ý thô sơ và cố ý <b>không</b> kéo thêm thư viện phân tích User-Agent: chuỗi này chỉ
    /// dùng để người quản trị nhận ra máy nào là máy nào trước khi bấm thu hồi, không phải để
    /// thống kê. Nhận không ra thì trả nguyên chuỗi gốc — nói "Không rõ" trong khi vẫn còn
    /// thông tin trong tay là giấu mất manh mối duy nhất họ có.
    /// </para>
    /// </summary>
    private static string DescribeDevice(string? userAgent, string? browser, string? platform)
        => (browser, platform) switch
        {
            (not null, not null) => $"{browser} trên {platform}",
            (not null, null) => browser,
            (null, not null) => platform,
            _ => string.IsNullOrWhiteSpace(userAgent) ? "Không rõ thiết bị" : userAgent
        };

    /// <summary>
    /// Tách <c>User-Agent</c> thành trình duyệt và hệ điều hành.
    /// <para>
    /// Thứ tự các phép hỏi ở đây là <b>chỗ duy nhất có logic thật</b> trong lớp này, nên đừng
    /// sắp lại cho gọn mắt: xem hai chú thích bên trong.
    /// </para>
    /// </summary>
    private static (string? Browser, string? Os) ParseUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return (null, null);

        var browser =
            userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge"
            : userAgent.Contains("Brave", StringComparison.OrdinalIgnoreCase) ? "Brave"
            : userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase) ? "Opera"
            : userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase) ? "Firefox"
            // Chrome phải hỏi sau Edge, Brave và Opera: cả ba đều dựng trên Chromium nên
            // chuỗi của chúng đều chứa "Chrome". Hỏi trước thì mọi trình duyệt hóa ra Chrome.
            : userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ? "Chrome"
            // Safari phải hỏi sau cùng, vì mọi trình duyệt Chromium cũng mang chữ "Safari".
            : userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase) ? "Safari"
            : null;

        var platform =
            userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows"
            : userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android"
            // iPhone và iPad phải hỏi trước "Mac": chuỗi của iOS cũng chứa "like Mac OS X".
            : userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ? "iPhone"
            : userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iPad"
            : userAgent.Contains("Mac", StringComparison.OrdinalIgnoreCase) ? "macOS"
            : userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux"
            : null;

        return (browser, platform);
    }
}
