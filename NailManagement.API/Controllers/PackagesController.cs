using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.Features.Packages.UseCases;
using NailManagement.Domain.Access;

namespace NailManagement.API.Controllers;

/// <summary>
/// Bảng giá gói dịch vụ — chỉ đọc, chỉ Superadmin.
/// <para>
/// Chỉ có động từ GET, giống <c>AuditLogsController</c> và cũng vì một lý do có thật: module
/// quản lý gói đã bị cắt khỏi MVP. Những gì vắng mặt ở đây nói đúng phạm vi đã chốt.
/// </para>
/// <para>
/// Nó tồn tại vì màn tạo tiệm cần bảng giá thật: BR-TENANT-004 bắt chọn gói ngay lúc tạo,
/// và BR-SUB-004 chốt giá cùng số phiên bản tại thời điểm đó. Không có endpoint này thì màn
/// hình phải chọn từ dữ liệu mẫu, và tiệm mới sẽ trỏ tới một mã gói không có trong database.
/// </para>
/// </summary>
[ApiController]
[Route("api/packages")]
public sealed class PackagesController(ListPackagesUseCase listPackages) : ControllerBase
{
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.Packages, RequiresTenant = false)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(new { packages = await listPackages.ExecuteAsync(cancellationToken) });
}
