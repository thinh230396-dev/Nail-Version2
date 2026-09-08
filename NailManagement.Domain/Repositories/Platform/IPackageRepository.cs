using NailManagement.Domain.Entities.Platform;

namespace NailManagement.Domain.Repositories.Platform;

/// <summary>
/// Cổng ra bảng giá gói dịch vụ.
/// <para>
/// Chỉ có phần đọc. Module quản lý gói (tạo, sửa, ngừng bán) đã bị cắt khỏi phạm vi MVP,
/// nhưng phần đọc thì bắt buộc phải có: BR-TENANT-004 yêu cầu chọn gói ngay lúc tạo tiệm,
/// và BR-SUB-004 yêu cầu chốt giá cùng số phiên bản của gói tại đúng thời điểm đó.
/// </para>
/// </summary>
public interface IPackageRepository
{
    Task<Package?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Bảng giá, sắp theo giá tăng dần để màn hình không phải tự sắp lại.</summary>
    Task<IReadOnlyList<Package>> ListAsync(CancellationToken cancellationToken = default);
}
