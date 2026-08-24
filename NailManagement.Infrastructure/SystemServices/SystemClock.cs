using NailManagement.Application.Abstractions;

namespace NailManagement.Infrastructure.SystemServices;

/// <summary>Đồng hồ thật. Test dùng bản cài đặt khác trả về một thời điểm cố định.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
