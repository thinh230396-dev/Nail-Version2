using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.UseCases.Reports;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.API.Controllers;

/// <summary>
/// Báo cáo doanh thu của tiệm đang làm việc — BR-REV-001…008.
///
/// <para>
/// <b>Chỉ chủ tiệm.</b> Ô <c>RevenueReports</c> trong ma trận mục 3.4 để trống cho cả hai vai
/// còn lại, và vì hai lý do khác nhau: Superadmin không chạm được dữ liệu nghiệp vụ bên trong
/// tiệm (BR-AUTH-030), còn lễ tân thì doanh thu tiệm không phải việc của quầy — họ thu tiền cả
/// ngày nhưng không cần biết tháng này tiệm lãi bao nhiêu.
/// </para>
/// <para>
/// <b>Một endpoint, bốn chiều</b> — quyết định 60 ngày 16. §9.1 dự trù 4 endpoint, nhưng màn
/// báo cáo vẽ một trang cần cả bốn cùng lúc: bốn lời gọi là bốn lần quét cùng một khoảng dữ
/// liệu, và tệ hơn, chúng có thể rơi vào hai phía của một lần thu tiền ở quầy — khi đó bốn
/// bảng trên cùng màn hình cộng ra bốn con số khác nhau mà không ai giải thích được.
/// </para>
/// <para>
/// Cố ý KHÔNG có endpoint xuất file hay lịch gửi định kỳ: BR-REV-006 nói rõ API chỉ cung cấp
/// endpoint tổng hợp, còn báo cáo nâng cao nằm ngoài phạm vi.
/// </para>
/// </summary>
[ApiController]
[Route("api/reports")]
public sealed class ReportsController(GetRevenueReportUseCase revenueReport) : ControllerBase
{
    /// <summary>
    /// Doanh thu theo <b>tiền thực thu</b> trong một khoảng, kèm bốn bảng phân rã.
    /// </summary>
    /// <param name="branchId">
    /// Bộ lọc chi nhánh do người dùng chọn trên màn hình. Khác mọi endpoint khác của hệ thống,
    /// tham số này <b>được phép</b> đến từ chuỗi truy vấn: endpoint chỉ chủ tiệm gọi được, mà
    /// chủ tiệm thì nhìn cả tiệm — nên đây là một phép lọc, không phải ranh giới cách ly.
    /// </param>
    [HttpGet("revenue")]
    [RequireAuth]
    [RequirePermission(Feature.RevenueReports)]
    public async Task<IActionResult> Revenue(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? branchId,
        CancellationToken cancellationToken)
        => Ok(new { report = await revenueReport.ExecuteAsync(from, to, branchId, cancellationToken) });
}
