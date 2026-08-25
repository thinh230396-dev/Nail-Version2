using NailManagement.Application.Common.Exceptions;

namespace NailManagement.API.Security;

/// <summary>
/// Chặn mọi thao tác ghi khi tiệm đã hết hạn hoặc bị khóa — BR-TENANT-010/012.
/// <para>
/// BR-TENANT-012 nói rõ: lệnh chặn này cài ở <b>một middleware duy nhất</b>, và frontend
/// không cần biết luật tồn tại. Rải phép kiểm tra vào từng use case là cách chắc chắn để
/// một use case viết sau quên mất nó, và khi đó tiệm hết hạn vẫn ghi được — đúng cái lỗ mà
/// cả cơ chế này sinh ra để bịt.
/// </para>
/// <para>
/// Đây là <b>bước 1</b> trong bốn bước của BR-TENANT-013 nên nó chạy TRƯỚC bộ lọc quyền:
/// tiệm hết hạn phải nhận thông báo "cần gia hạn", không phải "không có quyền", bất kể gói
/// nào và vai trò nào.
/// </para>
/// <para>
/// Chỉ chặn ghi. BR-TENANT-010 giữ nguyên quyền xem toàn bộ dữ liệu: khóa cả phần đọc thì
/// tiệm hết hạn mất luôn sổ sách của chính mình, một hình phạt không ai đặt ra.
/// </para>
/// </summary>
public sealed class TenantWriteGuardMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> WriteMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public async Task InvokeAsync(HttpContext context, RequestScope scope)
    {
        if (WriteMethods.Contains(context.Request.Method) && scope.Context?.Tenant is { IsReadOnly: true })
        {
            // BR-TENANT-011 — phép miễn trừ đánh dấu ngay trên chính endpoint thay vì liệt
            // kê đường dẫn ở đây. Danh sách đường dẫn sẽ lệch ngay lần đầu ai đó đổi route
            // mà quên sửa danh sách.
            var exempt = context.GetEndpoint()?.Metadata
                .GetMetadata<AllowWhenTenantReadonlyAttribute>() is not null;

            if (!exempt) throw new TenantReadonlyException();
        }

        await next(context);
    }
}
