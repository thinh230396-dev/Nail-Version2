using NailManagement.API.Controllers;
using NailManagement.Application.UseCases.Auth;
using NailManagement.Domain.Common;
using NailManagement.Infrastructure.Persistence.TenantScope;

namespace NailManagement.API.Security;

/// <summary>
/// Đọc phiên đăng nhập từ cookie ở <b>mỗi request</b>, rồi dựng phạm vi làm việc cho phần
/// còn lại của chuỗi xử lý.
/// <para>
/// <b>BR-AUTH-022 sống hay chết ở đây.</b> Việc kiểm tra trạng thái tài khoản phải chạy mỗi
/// request chứ không chỉ lúc đăng nhập; đặt nhầm chỗ này thì tài khoản vừa bị khóa vẫn dùng
/// được tiếp cho tới khi phiên hết hạn. Phép kiểm tra thật nằm trong
/// <see cref="GetCurrentAccountUseCase"/> — middleware chỉ gọi lại, không chép lại.
/// </para>
/// <para>
/// Cookie hỏng hoặc phiên hết hạn <b>không</b> làm request thất bại ngay tại đây: request
/// chỉ trở thành ẩn danh. Nếu ném lỗi ở đây thì một cookie cũ còn sót sẽ chặn luôn cả lời
/// gọi đăng nhập, và người dùng mắc kẹt không đăng nhập lại được.
/// </para>
/// </summary>
public sealed class SessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RequestScope scope, AmbientTenantContext tenantScope)
    {
        var sessionId = context.Request.Cookies[AuthController.SessionCookieName];

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var resolve = context.RequestServices.GetRequiredService<GetCurrentAccountUseCase>();

            try
            {
                var account = await resolve.ExecuteAsync(sessionId, context.RequestAborted);
                scope.Attach(sessionId, account);

                // BR-ISO-002 — đây là chỗ DUY NHẤT trong toàn hệ thống đặt phạm vi tiệm cho
                // tầng dữ liệu. Giá trị này đã đi qua bước kiểm tra bảng UserTenants ở
                // BR-ISO-003 bên trong use case, nên tới đây nó đã đáng tin.
                tenantScope.SetActiveTenant(account.ActiveTenantId);
            }
            catch (AppException rejection)
            {
                // Phiên không dùng được nữa. Coi như chưa đăng nhập và đi tiếp; endpoint nào
                // cần quyền sẽ tự từ chối bằng bộ lọc RequireAuth.
                //
                // Lý do được giữ lại để bộ lọc đó ném ra đúng câu chữ: "tài khoản đã bị khóa"
                // khác hẳn "phiên hết hạn" dưới mắt người dùng, và gộp chung thì người bị
                // khóa tài khoản cứ đăng nhập lại mãi mà không hiểu vì sao.
                scope.Reject(rejection);
            }
        }

        await next(context);
    }
}
