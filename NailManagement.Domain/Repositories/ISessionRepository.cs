using NailManagement.Domain.Entities;

namespace NailManagement.Domain.Repositories;

/// <summary>
/// Cổng (port) ra kho dữ liệu phiên đăng nhập.
/// <para>
/// Cố ý không có hàm xóa: đăng xuất là <b>thu hồi</b> phiên (<c>RevokedAt</c>), không phải
/// xóa bản ghi — nhất quán với BR-DEL-001.
/// </para>
/// </summary>
public interface ISessionRepository
{
    Task AddAsync(AppSession session, CancellationToken cancellationToken = default);

    Task<AppSession?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    Task UpdateAsync(AppSession session, CancellationToken cancellationToken = default);
}
