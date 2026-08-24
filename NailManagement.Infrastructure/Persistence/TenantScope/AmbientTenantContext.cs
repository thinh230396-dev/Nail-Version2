using NailManagement.Application.Abstractions;

namespace NailManagement.Infrastructure.Persistence.TenantScope;

/// <summary>
/// Bản cài đặt của <see cref="ITenantContext"/>, sống theo vòng đời một request.
/// <para>
/// Tách phần ĐẶT giá trị ra khỏi giao diện mà tầng Application nhìn thấy là có chủ đích:
/// use case chỉ đọc được tiệm đang làm việc, còn quyền đổi nó nằm ở đúng hai chỗ — middleware
/// phiên đăng nhập (ngày 3) và bộ nạp dữ liệu mẫu. Nhờ vậy không có đường nào để một
/// endpoint tự trỏ sang tiệm khác giữa chừng.
/// </para>
/// </summary>
public sealed class AmbientTenantContext : ITenantContext
{
    public string? ActiveTenantId { get; private set; }

    /// <summary>
    /// Đặt tiệm đang làm việc. Người gọi PHẢI kiểm tra tài khoản có liên kết với tiệm này
    /// trong bảng <c>UserTenants</c> trước khi gọi — đó là bước 2 của BR-ISO-003, và bỏ nó
    /// là mở đường truy cập chéo tiệm.
    /// </summary>
    public void SetActiveTenant(string? tenantId) => ActiveTenantId = tenantId;

    /// <summary>
    /// Tạm chuyển sang một tiệm khác rồi tự trả lại giá trị cũ khi khối lệnh kết thúc.
    /// Dùng khi một tiến trình phải đi qua nhiều tiệm, ví dụ bộ nạp dữ liệu mẫu.
    /// </summary>
    public IDisposable EnterTenant(string? tenantId)
    {
        var previous = ActiveTenantId;
        ActiveTenantId = tenantId;

        return new Scope(this, previous);
    }

    private sealed class Scope(AmbientTenantContext owner, string? previous) : IDisposable
    {
        public void Dispose() => owner.ActiveTenantId = previous;
    }
}
