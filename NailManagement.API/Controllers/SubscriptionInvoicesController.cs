using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.Features.Subscriptions.UseCases;
using NailManagement.Domain.Access;

namespace NailManagement.API.Controllers;

/// <summary>
/// Hóa đơn đăng ký — BR-INV-001, thứ <b>tiệm trả cho SalonSys</b>.
///
/// <para>
/// ⚠️ Đường dẫn là <c>/api/subscription-invoices</c>, cố ý dài và cố ý khác
/// <c>/api/sales-invoices</c>. Hai bảng hóa đơn tách hoàn toàn, và BR-REV-008 nói rõ doanh thu
/// nền tảng tính từ bảng NÀY chứ không phải từ doanh thu bán hàng của tiệm — nhầm hai thứ đó là
/// nhầm luôn ý nghĩa của mọi con số trên màn Tổng quan của Superadmin.
/// </para>
/// <para>
/// Chỉ có động từ <c>GET</c>. Lát cắt gói đăng ký đã bị cắt khỏi MVP (§0 mục 13) nên không có
/// lệnh nộp chứng từ hay xác nhận thanh toán; endpoint này tồn tại vì thiếu nó thì màn Tổng quan
/// phải bịa ra con số doanh thu nền tảng.
/// </para>
/// </summary>
[ApiController]
[Route("api/subscription-invoices")]
public sealed class SubscriptionInvoicesController(
    ListSubscriptionInvoicesUseCase listInvoices,
    RequestScope requestScope) : ControllerBase
{
    /// <summary>
    /// Hóa đơn đăng ký trong tầm nhìn của người gọi: Superadmin thấy mọi tiệm, chủ tiệm chỉ
    /// thấy tiệm mình đang làm việc (BR-INV-032).
    ///
    /// <para>
    /// <c>RequiresTenant = false</c> vì tài khoản Superadmin không thuộc tiệm nào (BR-AUTH-031),
    /// giống các endpoint tầng nền tảng khác. Việc chủ tiệm bắt buộc phải chọn tiệm trước do
    /// use case tự đòi — cùng lối với nhật ký kiểm toán và danh sách phiên đăng nhập.
    /// </para>
    /// </summary>
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.SubscriptionInvoices, RequiresTenant = false)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var current = requestScope.Require();

        var invoices = await listInvoices.ExecuteAsync(
            current.Role,
            current.ActiveTenantId,
            cancellationToken);

        return Ok(new { invoices });
    }
}
