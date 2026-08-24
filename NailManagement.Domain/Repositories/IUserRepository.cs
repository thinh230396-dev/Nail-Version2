using NailManagement.Domain.Entities;

namespace NailManagement.Domain.Repositories;

/// <summary>
/// Cổng (port) ra kho dữ liệu tài khoản.
/// <para>
/// Đây là interface do tầng Domain đặt ra và tầng Infrastructure phải tuân theo — chính là
/// chỗ đảo ngược phụ thuộc của Clean Architecture. Domain không biết dữ liệu nằm ở
/// SQL Server, SQLite hay trong bộ nhớ.
/// </para>
/// <para>
/// Cố ý trả về entity chứ không trả <c>IQueryable</c>: để lộ <c>IQueryable</c> ra ngoài là
/// để EF Core rò rỉ qua ranh giới tầng, và khi đó tầng trong lại phụ thuộc vào tầng ngoài.
/// </para>
/// </summary>
public interface IUserRepository
{
    /// <summary>Tìm theo email hoặc username, không phân biệt hoa thường. Dùng khi đăng nhập.</summary>
    Task<AppUser?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    Task<AppUser?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    Task AddAsync(AppUser user, CancellationToken cancellationToken = default);

    /// <summary>Ghi lại thay đổi trên một tài khoản đã được theo dõi.</summary>
    Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
