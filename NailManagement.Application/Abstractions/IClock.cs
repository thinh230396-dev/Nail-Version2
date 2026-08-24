namespace NailManagement.Application.Abstractions;

/// <summary>
/// Cổng lấy thời gian hiện tại.
/// <para>
/// Không gọi thẳng <c>DateTimeOffset.UtcNow</c> trong use case: mọi quy tắc phụ thuộc thời
/// gian của hệ thống — hết hạn phiên, khóa tài khoản, tenant hết hạn (BR-TENANT-002), lịch
/// hẹn chồng giờ (BR-APT-011) — đều tính lúc đọc, nên phải kiểm thử được bằng cách bơm một
/// thời điểm cố định.
/// </para>
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
