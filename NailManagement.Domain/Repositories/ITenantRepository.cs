using NailManagement.Domain.Entities;

namespace NailManagement.Domain.Repositories;

/// <summary>
/// Cổng ra kho dữ liệu tiệm.
/// <para>
/// Các hàm ở đây trả về tiệm KÈM gói đăng ký, vì mọi lần dùng đều cần cả hai: xác định
/// tiệm còn hạn không (BR-TENANT-010) và gói có mở tính năng không (BR-SUB-007) là hai
/// bước liền nhau trong chuỗi kiểm tra quyền ở BR-TENANT-013.
/// </para>
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Đọc nhiều tiệm cùng lúc cho màn chọn tiệm, giữ nguyên thứ tự theo tên.</summary>
    Task<IReadOnlyList<Tenant>> ListByIdsAsync(
        IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);
}
