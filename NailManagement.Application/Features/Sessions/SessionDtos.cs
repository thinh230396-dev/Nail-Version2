using NailManagement.Domain.Access;
using NailManagement.Domain.Auth;

namespace NailManagement.Application.Features.Sessions;

/// <summary>
/// Một phiên đăng nhập nhìn từ màn quản trị — BR-AUTH-032.
/// <para>
/// Chỉ mang những gì bảng <c>AppSessions</c> thật sự lưu. Chú thích trên chính thực thể
/// <c>AppSession</c> đã chốt điều này từ ngày dựng bảng: <i>"Cố ý không có Location, Trusted,
/// Suspicious, MfaVerified — ba thứ đó không có nguồn dữ liệu thật, hiển thị chúng là bịa nội
/// dung."</i> DTO này giữ đúng giao kèo ấy.
/// </para>
/// <para>
/// <c>Device</c> là ngoại lệ duy nhất và nó <b>không phải dữ liệu mới</b>: nó là chuỗi
/// <c>UserAgent</c> đã được rút gọn cho người đọc. Chuỗi gốc vẫn đi kèm ở <c>UserAgent</c> để
/// màn hình hiện được khi cần đối chiếu.
/// </para>
/// </summary>
public sealed record SessionDto(
    string Id,
    string UserId,
    string UserDisplayName,
    string UserEmail,
    string UserRole,
    /// <summary>Tiệm phiên đang làm việc lúc đọc. Rỗng khi tài khoản chưa chọn tiệm nào.</summary>
    string? ActiveTenantId,
    string? Ip,
    string? UserAgent,
    /// <summary>Tên thiết bị rút gọn từ <c>UserAgent</c>, ví dụ "Chrome trên Windows".</summary>
    string Device,
    /// <summary>Trình duyệt tách riêng, vì màn hình hiện nó ở một cột khác với hệ điều hành.</summary>
    string? Browser,
    /// <summary>Hệ điều hành tách riêng. Rỗng khi không nhận ra từ <c>UserAgent</c>.</summary>
    string? Os,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActive,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    /// <summary>
    /// <c>ACTIVE</c> · <c>EXPIRED</c> · <c>REVOKED</c> — tính <b>lúc đọc</b>, không phải một cột
    /// được lưu. BR-TENANT-003 nói toàn hệ thống không có job chạy nền, nên một phiên hết hạn
    /// vẫn nằm nguyên trong bảng cho tới khi có người hỏi tới nó.
    /// </summary>
    string Status,
    /// <summary>Đúng khi đây chính là phiên mà người gọi đang dùng — BR-AUTH-033.</summary>
    bool IsCurrent);
