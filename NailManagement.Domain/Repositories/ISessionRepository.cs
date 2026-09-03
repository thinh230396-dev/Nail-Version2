using NailManagement.Domain.Entities.Auth;

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

    /// <summary>
    /// Danh sách phiên để quản trị, kèm sẵn hồ sơ chủ phiên — BR-AUTH-032.
    /// </summary>
    /// <param name="tenantId">
    /// Rỗng thì lấy mọi phiên (Superadmin). Có giá trị thì chỉ lấy phiên của những tài khoản
    /// <b>có liên kết với tiệm ấy</b> qua bảng <c>UserTenants</c>.
    /// <para>
    /// Cố ý lọc theo chủ tài khoản chứ không theo <c>ActiveTenantId</c> của phiên: một tài
    /// khoản quản nhiều tiệm thì phiên của họ có thể đang trỏ sang tiệm khác, nhưng người đó
    /// vẫn là người của tiệm này và chủ tiệm vẫn phải thấy để đóng được. Lọc theo
    /// <c>ActiveTenantId</c> sẽ giấu mất đúng những phiên đáng lo nhất.
    /// </para>
    /// </param>
    Task<IReadOnlyList<AppSession>> ListAsync(
        string? tenantId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra một tài khoản có thuộc tiệm cho trước không — dùng để chặn chủ tiệm thu hồi
    /// phiên của người ngoài tiệm mình (BR-AUTH-033).
    /// </summary>
    Task<bool> UserBelongsToTenantAsync(
        string userId, string tenantId, CancellationToken cancellationToken = default);
}
