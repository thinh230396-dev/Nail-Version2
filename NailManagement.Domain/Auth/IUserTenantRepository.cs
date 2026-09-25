namespace NailManagement.Domain.Auth;

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

    /// <summary>
    /// Các tài khoản chủ tiệm được giao cho từng tiệm, tra một lượt cho cả danh sách.
    /// <para>
    /// Gộp thành một lượt thay vì hỏi từng tiệm một, vì màn quản lý tiệm của Superadmin
    /// hiển thị chủ tiệm ngay trên mỗi dòng — hỏi lẻ là mỗi lần mở màn hình lại tốn đúng
    /// bằng số tiệm lượt truy vấn.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<AppUser>>> ListOwnersAsync(
        IReadOnlyCollection<string> tenantIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chiều ngược lại của <see cref="ListOwnersAsync"/>: mỗi tài khoản đang giữ những tiệm
    /// nào, tra một lượt cho cả danh sách tài khoản.
    /// <para>
    /// Màn quản lý tài khoản chủ tiệm hiển thị số tiệm ngay trên mỗi dòng, và ô chọn chủ
    /// tiệm ở form tạo tiệm cần biết ai đang rảnh. Cả hai đều hỏi cùng một câu, nên chỉ nên
    /// có một lượt truy vấn trả lời.
    /// </para>
    /// <para>
    /// Tài khoản không giữ tiệm nào <b>không có mặt</b> trong kết quả, thay vì có mặt với
    /// danh sách rỗng. Người gọi phải tự hiểu vắng mặt là không có tiệm nào — đó cũng là
    /// cách <see cref="ListOwnersAsync"/> hành xử, giữ cho hai hàm đọc giống nhau.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ListTenantIdsByUserAsync(
        IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);

    Task LinkAsync(string userId, string tenantId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// BR-TENANT-021 — xóa mềm một tiệm chỉ gỡ liên kết ở bảng này. Tài khoản chủ tiệm vẫn
    /// tồn tại và vẫn đăng nhập được, chỉ mất quyền với tiệm vừa xóa; họ chỉ bị chặn đăng
    /// nhập khi không còn tiệm nào.
    /// </summary>
    Task UnlinkAllAsync(string tenantId, CancellationToken cancellationToken = default);
}
