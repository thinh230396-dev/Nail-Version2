using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Repositories;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Bản cài đặt <see cref="IUserRepository"/> bằng EF Core.
/// <para>
/// Đây là chỗ duy nhất biết tới <c>DbContext</c>. Tầng Application chỉ thấy interface, nên
/// khi test có thể thay bằng một bản cài đặt trong bộ nhớ mà không đổi một dòng use case nào.
/// </para>
/// </summary>
public sealed class UserRepository(NailDbContext db) : IUserRepository
{
    public async Task<AppUser?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var normalized = (identifier ?? string.Empty).Trim().ToLowerInvariant();

        // Dựng đối tượng Email TRƯỚC khi vào biểu thức LINQ. Nếu gọi Email.FromPersistence
        // ngay trong biểu thức, EF Core sẽ cố dịch lời gọi hàm đó sang SQL và thất bại.
        // Tạo sẵn ở đây thì nó trở thành hằng số được bắt, và bộ chuyển đổi giá trị lo
        // phần đổi sang chuỗi.
        var emailCandidate = Email.FromPersistence(normalized);

        return await db.AppUsers.FirstOrDefaultAsync(
            u => u.Email == emailCandidate
                 || (u.Username != null && u.Username.ToLower() == normalized),
            cancellationToken);
    }

    public async Task<AppUser?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        await db.AppUsers.AddAsync(user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        db.AppUsers.Update(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await db.AppUsers.CountAsync(cancellationToken);

    /// <summary>
    /// Sắp theo tên hiển thị ngay trong câu truy vấn để thứ tự do database quyết định một
    /// lần, thay vì mỗi màn hình tự sắp lại theo cách của mình rồi ra kết quả khác nhau.
    /// </summary>
    public async Task<IReadOnlyList<AppUser>> ListByRoleAsync(
        UserRole role, CancellationToken cancellationToken = default)
        => await db.AppUsers
            .Where(user => user.Role == role)
            .OrderBy(user => user.DisplayName)
            .ToListAsync(cancellationToken);
}
