using Microsoft.AspNetCore.Mvc.Filters;
using NailManagement.Application.Common.Exceptions;

namespace NailManagement.API.Security;

/// <summary>
/// Endpoint bắt buộc phải có phiên đăng nhập hợp lệ, nhưng không gắn với nhóm chức năng nào
/// trong ma trận phân quyền.
/// <para>
/// Dùng cho đúng những việc mà mọi vai trò đã đăng nhập đều làm được: đọc thông tin tài
/// khoản của chính mình, xem danh sách tiệm mình quản lý, chọn tiệm để làm việc. Mọi
/// endpoint nghiệp vụ khác phải dùng <see cref="RequirePermissionAttribute"/>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireAuthAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var scope = context.HttpContext.RequestServices.GetRequiredService<RequestScope>();

        if (!scope.IsAuthenticated) throw scope.Rejection ?? new UnauthenticatedException();
    }
}
