namespace NailManagement.Domain.Repositories;

/// <summary>
/// Cổng ra bảng nối tài khoản với tiệm — BR-AUTH-023.
/// <para>
/// Đây là cổng của bước 2 trong BR-ISO-003: trước khi lọc dữ liệu theo tiệm, phải xác nhận
/// tài khoản thật sự có quyền với tiệm đó. Bỏ bước này thì chỉ cần sửa mã tiệm trong phiên
/// là đọc được dữ liệu của tiệm khác.
/// </para>
/// </summary>
public interface IUserTenantRepository
{
    /// <summary>Tài khoản có được giao quản lý tiệm này không.</summary>
    Task<bool> HasAccessAsync(string userId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Danh sách mã tiệm mà tài khoản quản lý, dùng cho màn chọn tiệm.</summary>
    Task<IReadOnlyList<string>> ListTenantIdsAsync(string userId, CancellationToken cancellationToken = default);

    Task LinkAsync(string userId, string tenantId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
