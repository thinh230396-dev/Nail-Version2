using NailManagement.Application.Abstractions;
using NailManagement.Domain.Access;
using NailManagement.Domain.Auth;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Infrastructure.Persistence.Seed;

/// <summary>
/// Tạo tài khoản quản trị <b>đầu tiên</b> từ cấu hình, cho những môi trường không được nạp dữ
/// liệu demo.
///
/// <para>
/// Là vế thứ hai của lần vá ngày 24. Vế thứ nhất — <c>DemoSeedPolicy</c> — chặn tài khoản demo
/// có mật khẩu cố định sinh ra ngoài máy phát triển. Nhưng chặn một mình thì một bản triển khai
/// trên database trống sẽ <b>không có đường nào để đăng nhập lần đầu</b>, nên phải có lối vào
/// thay thế, và lối ấy lấy mật khẩu từ biến môi trường chứ không từ mã nguồn.
/// </para>
/// <para>
/// Khác <see cref="DemoAccountSeeder"/> ở ba điểm, và cả ba đều có lý do: chỉ tạo <b>một</b> tài
/// khoản Superadmin (hai vai trò kia thuộc về dữ liệu nghiệp vụ, không thuộc về việc mở máy);
/// mật khẩu đến từ bên ngoài; và không kèm bất kỳ tiệm, chi nhánh hay khách hàng nào.
/// </para>
/// </summary>
public sealed class BootstrapAdminSeeder(
    IUserRepository users,
    IPasswordHasher hasher,
    IClock clock)
{
    /// <summary>
    /// Tạo tài khoản nếu bảng tài khoản còn trống, và trả về <c>true</c> khi đã tạo.
    ///
    /// <para>
    /// Điều kiện "còn trống" giống hệt bộ nạp demo, và cũng vì lý do đó: chạy lại máy chủ không
    /// được ghi đè mật khẩu người dùng đã đổi, cũng không được mở lại tài khoản đã bị khóa.
    /// </para>
    /// <para>
    /// Mật khẩu đi qua <see cref="RawPassword"/> nên vẫn phải đạt chính sách BR-AUTH-002 như mọi
    /// mật khẩu khác. Một biến môi trường đặt sai sẽ ném lỗi ngay lúc khởi động — cố ý: hỏng ồn
    /// ào lúc mở máy tốt hơn là im lặng bỏ qua rồi để lại một hệ thống không ai vào được.
    /// </para>
    /// </summary>
    public async Task<bool> SeedAsync(
        string email,
        string password,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        if (await users.CountAsync(cancellationToken) > 0) return false;

        var address = Email.Create(email);
        var hashed = hasher.Hash(RawPassword.Create(password));

        // Tên đăng nhập lấy từ phần trước dấu @ để người vận hành có thể đăng nhập bằng một
        // trong hai thứ, đúng như mọi tài khoản khác. Bảng đang trống nên không thể trùng.
        var username = email.Split('@')[0].Trim().ToLowerInvariant();

        var admin = AppUser.Create(
            "USR-BOOTSTRAP",
            address,
            username,
            hashed.Hash,
            hashed.Salt,
            UserRole.SuperAdmin,
            string.IsNullOrWhiteSpace(displayName) ? "Quản trị hệ thống" : displayName.Trim(),
            clock.UtcNow);

        await users.AddAsync(admin, cancellationToken);

        return true;
    }
}
