using NailManagement.Domain.Entities;

namespace NailManagement.Domain.Repositories;

/// <summary>Cổng ra kho dữ liệu nhân viên.</summary>
public interface IStaffRepository
{
    /// <summary>
    /// Đọc hồ sơ nhân viên kèm chi nhánh, phục vụ việc dựng phạm vi cho phiên đăng nhập.
    /// <para>
    /// ⚠️ Hàm này chạy <b>trước khi</b> phạm vi tiệm của request được thiết lập — nó chính
    /// là một phần của việc thiết lập đó — nên nó phải bỏ qua bộ lọc theo tiệm, thứ mà mọi
    /// truy vấn khác đều đi qua.
    /// </para>
    /// <para>
    /// Vì vậy người gọi <b>bắt buộc</b> phải tự đối chiếu <c>TenantId</c> của hồ sơ trả về
    /// với tiệm đang làm việc của phiên. Đây là chỗ duy nhất trong hệ thống mà phép cách ly
    /// tenant không tự động, nên nó được viết rõ ở cả hai đầu: ở đây và ở use case gọi tới.
    /// </para>
    /// </summary>
    Task<Staff?> FindForSessionAsync(string staffId, CancellationToken cancellationToken = default);
}
