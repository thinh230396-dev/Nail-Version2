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

    /// <summary>
    /// Tài khoản đăng nhập của một loạt hồ sơ nhân viên, tra một lượt.
    /// <para>
    /// Bảng tài khoản KHÔNG mang <c>ITenantOwned</c> nên không có bộ lọc theo tiệm ở đây.
    /// Phép cách ly đến từ chính danh sách mã hồ sơ mà người gọi truyền vào: use case đã lấy
    /// chúng từ kho dữ liệu nhân viên, tức đã đi qua bộ lọc theo tiệm rồi.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, AppUser>> ListByStaffIdsAsync(
        IReadOnlyCollection<string> staffIds, CancellationToken cancellationToken = default)
    {
        if (staffIds.Count == 0) return new Dictionary<string, AppUser>();

        var ids = staffIds.ToArray();

        var accounts = await db.AppUsers
            .Where(user => user.StaffId != null && ids.Contains(user.StaffId))
            .ToListAsync(cancellationToken);

        // Mỗi hồ sơ nhiều nhất một tài khoản (BR-AUTH-013), nên gom thẳng thành từ điển
        // được. Nếu dữ liệu lỗi có hai tài khoản cùng trỏ một hồ sơ thì ToDictionary sẽ ném
        // lỗi ngay tại đây — đó là điều mong muốn, im lặng chọn một cái mới là tệ.
        return accounts.ToDictionary(user => user.StaffId!);
    }

    public async Task<AppUser?> FindByStaffIdAsync(
        string staffId, CancellationToken cancellationToken = default)
        => await db.AppUsers.FirstOrDefaultAsync(user => user.StaffId == staffId, cancellationToken);
}
