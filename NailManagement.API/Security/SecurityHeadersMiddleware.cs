namespace NailManagement.API.Security;

/// <summary>
/// Ba header bảo mật cho mọi phản hồi, API lẫn giao diện dựng sẵn.
/// <list type="bullet">
///   <item><c>X-Content-Type-Options: nosniff</c> — trình duyệt không đoán lại kiểu nội dung, nên
///   một chuỗi JSON do người dùng nhập không bị chạy như script.</item>
///   <item><c>X-Frame-Options: DENY</c> — không trang nào nhúng được giao diện vào iframe để lừa
///   người ở quầy bấm "Hoàn tiền".</item>
///   <item><c>Referrer-Policy: no-referrer</c> — đường dẫn nội bộ không đi theo khi bấm ra ngoài.</item>
/// </list>
/// <para>
/// Không đặt Content-Security-Policy ở đây: chính sách ấy phụ thuộc cách giao diện được build,
/// và một CSP sai làm trắng màn hình. Nó thuộc về lúc cấu hình bản triển khai giao diện.
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        // Đặt ngay trước khi gửi header, không phải lúc request đi vào: bộ xử lý lỗi gọi
        // Response.Clear() — xóa luôn header đã đặt sớm — nên phản hồi lỗi sẽ thiếu chúng.
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            return Task.CompletedTask;
        });

        return next(context);
    }
}
