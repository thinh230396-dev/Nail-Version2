using Microsoft.AspNetCore.Diagnostics;
using NailManagement.Domain.Common;

namespace NailManagement.API.Common;

/// <summary>
/// Điểm xử lý lỗi duy nhất của tầng HTTP, và <b>nơi duy nhất trong hệ thống biết tới HTTP
/// status</b>.
/// <para>
/// Tầng Domain và Application chỉ ném ra <see cref="AppException"/> mang mã lỗi nghiệp vụ;
/// chúng không cần biết HTTP tồn tại. Đây là vai trò "presenter" trong Clean Architecture.
/// </para>
/// <para>
/// Ghi chú về 401: <c>INVALID_CREDENTIALS</c> và <c>UNAUTHENTICATED</c> cùng trả 401 nhưng
/// mang mã khác nhau, vì frontend xử lý hai việc khác nhau — sai mật khẩu thì hiện lỗi ngay
/// tại form, còn phiên hết hạn thì đưa về màn đăng nhập.
/// </para>
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Dictionary<string, int> StatusByCode = new()
    {
        [ErrorCode.ValidationFailed] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCode.InvalidCredentials] = StatusCodes.Status401Unauthorized,
        [ErrorCode.AccountLocked] = StatusCodes.Status423Locked,
        [ErrorCode.AccountNotActive] = StatusCodes.Status403Forbidden,
        [ErrorCode.Unauthenticated] = StatusCodes.Status401Unauthorized,
        [ErrorCode.Forbidden] = StatusCodes.Status403Forbidden,

        // Cùng 403 với Forbidden, và cố ý: phân biệt nằm ở MÃ lỗi trong thân phản hồi, không
        // nằm ở mã HTTP. Xem chú thích ở ErrorCode.TenantNotSelected.
        [ErrorCode.TenantNotSelected] = StatusCodes.Status403Forbidden,
        [ErrorCode.NotFound] = StatusCodes.Status404NotFound,
        [ErrorCode.TenantReadonly] = StatusCodes.Status403Forbidden,
        [ErrorCode.LimitExceeded] = StatusCodes.Status409Conflict,
        [ErrorCode.SlotConflict] = StatusCodes.Status409Conflict,
        [ErrorCode.Internal] = StatusCodes.Status500InternalServerError
    };

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ErrorResponse payload;
        int status;

        if (exception is AppException appException)
        {
            status = StatusByCode.TryGetValue(appException.Code, out var mapped)
                ? mapped
                : StatusCodes.Status500InternalServerError;

            payload = new ErrorResponse(new ErrorBody(
                appException.Code,
                appException.Message,
                [.. appException.Fields.Select(f => new FieldErrorDto(f.Field, f.Message))]));
        }
        else
        {
            // Lỗi ngoài dự kiến: ghi đầy đủ vào log máy chủ nhưng chỉ trả ra ngoài một câu
            // chung. Stack trace lộ ra ngoài vừa vô dụng với người dùng vừa giúp ích cho
            // người dò lỗ hổng.
            logger.LogError(
                exception,
                "Lỗi không lường trước tại {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);

            status = StatusCodes.Status500InternalServerError;
            payload = new ErrorResponse(new ErrorBody(
                ErrorCode.Internal,
                "Máy chủ gặp sự cố. Vui lòng thử lại.",
                []));
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.Headers.CacheControl = "no-store";
        await httpContext.Response.WriteAsJsonAsync(payload, cancellationToken);

        return true;
    }
}
