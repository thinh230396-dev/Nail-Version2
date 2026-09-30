using NailManagement.Application.Features.Packages;
using NailManagement.Domain.Platform.Packages;

namespace NailManagement.Application.Features.Packages.UseCases;

/// <summary>
/// Bảng giá gói dịch vụ — chỉ đọc.
/// <para>
/// Module quản lý gói (tạo, sửa giá, ngừng bán) đã bị cắt khỏi MVP cùng với ngày 17 của lộ
/// trình. Phần đọc thì không cắt được: BR-TENANT-004 bắt Superadmin chọn gói ngay khi tạo
/// tiệm, và không có bảng giá thật thì màn tạo tiệm vẫn phải chọn từ dữ liệu mẫu — nghĩa là
/// tiệm mới sẽ trỏ tới một mã gói không tồn tại trong database.
/// </para>
/// </summary>
public sealed class ListPackagesUseCase(IPackageRepository packages)
{
    public async Task<IReadOnlyList<PackageDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var all = await packages.ListAsync(cancellationToken);

        return [.. all.Select(PackageMapper.ToDto)];
    }
}
