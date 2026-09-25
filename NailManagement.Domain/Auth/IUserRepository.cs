using NailManagement.Domain.Access;

namespace NailManagement.Domain.Auth;

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

    /// <summary>
    /// Mọi tài khoản của một vai trò, sắp theo tên hiển thị.
    /// <para>
    /// Có mặt vì BR-TENANT-004 cho phép Superadmin giao tiệm mới cho một tài khoản chủ tiệm
    /// <b>đã có</b>. Không có danh sách này thì giao diện chỉ còn đường tạo tài khoản mới,
    /// và khả năng một người quản nhiều tiệm (BR-AUTH-023) không có lối vào nào.
    /// </para>
    /// <para>
    /// Tài khoản đã khóa hoặc đã vô hiệu vẫn nằm trong kết quả: màn quản lý tài khoản phải
    /// nhìn thấy chúng, vì BR-DEL-001 không xóa cứng gì cả. Việc loại tài khoản không đủ
    /// điều kiện ra khỏi ô chọn chủ tiệm là quyết định của tầng gọi, không phải của kho dữ liệu.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AppUser>> ListByRoleAsync(UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tra nhiều tài khoản theo mã trong một lượt, khóa kết quả là <c>Id</c>.
    /// <para>
    /// Có mặt vì nhật ký kiểm toán chỉ lưu <b>mã</b> người thao tác (BR-AUD-003): tên đổi được
    /// còn mã thì không, nên một bản ghi lưu tên sẽ nói sai về quá khứ ngay lần đầu ai đó đổi
    /// tên. Cái giá là màn hình phải tự dịch mã sang tên lúc đọc, và dịch cho cả trang trong
    /// một lượt chứ không hỏi lẻ từng dòng — ba trăm bản ghi là ba trăm lượt đi database.
    /// </para>
    /// <para>
    /// Mã không tìm thấy thì <b>vắng mặt</b> khỏi từ điển thay vì có mặt với giá trị rỗng, cùng
    /// quy ước với <see cref="ListByStaffIdsAsync"/>. Chỗ gọi phải tự quyết hiển thị gì khi
    /// thiếu — và với nhật ký thì câu trả lời là giữ nguyên mã, vì một dòng nhật ký không có
    /// người thực hiện thì vô dụng.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, AppUser>> ListByIdsAsync(
        IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tài khoản đăng nhập gắn với từng hồ sơ nhân viên, tra một lượt cho cả danh sách.
    /// <para>
    /// Màn quản lý nhân viên phải biết hồ sơ nào đã có tài khoản thì mới quyết định được
    /// nút "Cấp tài khoản đăng nhập" hiện ở dòng nào (BR-AUTH-013). Hỏi lẻ từng hồ sơ là
    /// mỗi lần mở màn hình lại tốn đúng bằng số nhân viên lượt truy vấn.
    /// </para>
    /// <para>
    /// Khóa của kết quả là <c>StaffId</c>, và hồ sơ chưa có tài khoản thì <b>vắng mặt</b>
    /// thay vì có mặt với giá trị rỗng — giống cách <c>IUserTenantRepository</c> làm, để
    /// hai kho dữ liệu không có hai quy ước đọc khác nhau.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, AppUser>> ListByStaffIdsAsync(
        IReadOnlyCollection<string> staffIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tài khoản đăng nhập của đúng một hồ sơ nhân viên, hoặc null khi hồ sơ đó chưa được cấp.
    /// <para>
    /// Đi cùng <see cref="ListByStaffIdsAsync"/> chứ không thay thế nó: màn danh sách hỏi
    /// một lượt cho tất cả, còn các lệnh ghi chỉ hỏi về đúng hồ sơ đang sửa. Ép lệnh ghi
    /// dùng hàm danh sách là bắt nó dựng một tập hợp một phần tử rồi lại mở ra.
    /// </para>
    /// </summary>
    Task<AppUser?> FindByStaffIdAsync(string staffId, CancellationToken cancellationToken = default);
}
