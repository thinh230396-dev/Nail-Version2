using NailManagement.Domain.Entities.Auditing;

namespace NailManagement.Domain.Repositories;

/// <summary>
/// Cổng ra nhật ký kiểm toán.
/// <para>
/// BR-AUD-004 — nhật ký không sửa được và không xóa được, nên cổng này CHỈ có hàm ghi thêm.
/// Không có <c>Update</c>, không có <c>Delete</c>: quy tắc được diễn đạt bằng chính hình
/// dạng của interface, thay vì bằng một dòng chú thích mà người viết sau có thể bỏ qua.
/// </para>
/// </summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đọc nhật ký, mới nhất trước.
    /// <para>
    /// BR-AUD-005 — <paramref name="tenantId"/> rỗng nghĩa là đọc toàn hệ thống, và chỉ
    /// Superadmin mới được phép truyền như vậy. Phép kiểm tra vai trò nằm ở use case, vì
    /// kho dữ liệu không biết ai đang gọi mình.
    /// </para>
    /// <para>
    /// Bảng này cố ý KHÔNG mang bộ lọc tự động theo tiệm như các bảng nghiệp vụ khác:
    /// Superadmin phải đọc được toàn bộ, mà tài khoản Superadmin thì không thuộc tiệm nào.
    /// Đây là ngoại lệ duy nhất, và cái giá của nó là người gọi phải tự nêu phạm vi.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AuditLog>> ListAsync(
        string? tenantId, int take, CancellationToken cancellationToken = default);
}
