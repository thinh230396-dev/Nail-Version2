namespace NailManagement.Domain.Entities;

/// <summary>
/// Phiên đăng nhập lưu ở server.
/// <para>
/// Vì sao là phiên trong database chứ không phải JWT: BR-AUTH-022 bắt kiểm tra trạng thái
/// tài khoản ở MỖI request và cho phép vô hiệu phiên ngay lập tức; BR-AUTH-024 bắt lưu
/// tiệm đang làm việc trong phiên. JWT đã cấp thì không thu hồi được giữa chừng.
/// </para>
/// </summary>
public class AppSession
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private AppSession()
    {
        Id = string.Empty;
        UserId = string.Empty;
    }

    private AppSession(
        string id,
        string userId,
        DateTimeOffset now,
        TimeSpan lifetime,
        string? ip,
        string? userAgent)
    {
        Id = id;
        UserId = userId;
        CreatedAt = now;
        ExpiresAt = now.Add(lifetime);
        LastActive = now;
        RevokedAt = null;
        Ip = ip;
        UserAgent = userAgent;
        ActiveTenantId = null;
    }

    public string Id { get; private set; }
    public string UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    // Bốn thuộc tính dưới theo quyết định "mở rộng bảng phiên, KHÔNG tạo bảng phiên thứ hai".
    // Cố ý không có Location, Trusted, Suspicious, MfaVerified — ba thứ đó không có nguồn
    // dữ liệu thật, hiển thị chúng là bịa nội dung.
    public DateTimeOffset LastActive { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? Ip { get; private set; }
    public string? UserAgent { get; private set; }

    /// <summary>
    /// BR-AUTH-024 — tiệm đang làm việc nằm trong phiên, không gửi kèm mỗi request.
    /// Ngày 1 luôn null; ngày 3 mới có endpoint đặt giá trị này (BR-AUTH-025/026).
    /// </summary>
    public string? ActiveTenantId { get; private set; }

    public AppUser? User { get; private set; }

    public static AppSession Issue(
        string id,
        string userId,
        DateTimeOffset now,
        TimeSpan lifetime,
        string? ip,
        string? userAgent)
        => new(id, userId, now, lifetime, ip, userAgent);

    /// <summary>Còn dùng được: chưa hết hạn và chưa bị thu hồi.</summary>
    public bool IsValidAt(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Touch(DateTimeOffset now) => LastActive = now;

    /// <summary>
    /// BR-AUTH-025 — đổi tiệm đang làm việc bằng một thao tác trên PHIÊN, không phải bằng
    /// cách gửi kèm mã tiệm vào từng request.
    /// <para>
    /// BR-AUTH-026 — người gọi bắt buộc phải kiểm tra tài khoản có liên kết với tiệm này
    /// trong bảng <c>UserTenants</c> TRƯỚC khi gọi. Entity không tự kiểm tra được vì phép
    /// kiểm tra đó cần đọc bảng khác, mà entity thì không biết tới kho dữ liệu.
    /// </para>
    /// </summary>
    public void SetActiveTenant(string? tenantId, DateTimeOffset now)
    {
        ActiveTenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
        LastActive = now;
    }

    /// <summary>
    /// Đăng xuất là thu hồi, không xóa bản ghi — nhất quán với BR-DEL-001 (không có gì bị
    /// xóa cứng khỏi database) và giữ lại lịch sử phiên cho màn hình thu hồi phiên về sau.
    /// </summary>
    public void Revoke(DateTimeOffset now) => RevokedAt = now;

    /// <summary>Số giây còn lại, để tầng API đặt Max-Age cho cookie.</summary>
    public int RemainingSeconds(DateTimeOffset now)
        => (int)Math.Max(0, (ExpiresAt - now).TotalSeconds);
}
