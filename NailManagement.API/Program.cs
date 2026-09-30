using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using NailManagement.API.Common;
using NailManagement.API.Security;
using NailManagement.API.Startup;
using NailManagement.Application;
using NailManagement.Domain.Access;
using NailManagement.Domain.Shared;
using NailManagement.Infrastructure;
using NailManagement.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ── Tầng trong ────────────────────────────────────────────────────────────────
// API chỉ gọi hai hàm mở rộng này; nó không biết use case cần những gì, cũng không
// biết repository được cài đặt bằng gì. Đó là điểm ráp nối duy nhất của hệ thống.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Tầng ngoài ────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Frontend đang đọc account.displayName, account.tenantId… nên JSON phải là
        // camelCase. Đây cũng là mặc định của ASP.NET Core, khai báo lại cho rõ ý.
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Kết quả xác thực của request, do SessionMiddleware ghi vào và mọi tầng sau chỉ đọc.
builder.Services.AddScoped<RequestScope>();

// ── Giới hạn tần suất đăng nhập theo địa chỉ IP ───────────────────────────────
// Đếm theo NGUỒN GỌI, bổ sung cho phép khóa tạm vốn đếm theo TÀI KHOẢN. Hai bộ đếm bắt hai
// kiểu tấn công khác nhau, và thiếu cái nào cũng để hở một kiểu:
//
//   · Khóa theo tài khoản (5 lần / 15 phút, AuthPolicy) chặn người dò nhiều mật khẩu vào
//     MỘT tài khoản.
//   · Giới hạn theo IP chặn người rải MỘT mật khẩu phổ biến qua hàng loạt tài khoản khác
//     nhau — mỗi tài khoản chỉ sai đúng một lần, nên bộ đếm kia không bao giờ thấy gì.
//
// Con số mặc định 30 lần / 5 phút không phải chọn bừa: kịch bản `npm run rehearsal` gọi đăng
// nhập 10 lần từ cùng một IP, nên trần phải rộng hơn con số đó đủ để chạy lại vài lượt liên
// tiếp mà không tự khóa mình. Đặt qua cấu hình để bộ kiểm thử nới ra và để bản triển khai
// siết lại mà không phải sửa mã.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(RateLimitPolicy.Login, context =>
    {
        // Đọc cấu hình LÚC CÓ REQUEST, không phải lúc dựng builder.
        //
        // Đây là cái bẫy mà chú thích ở `SalonSysFactory` đã tả cho chuỗi kết nối, và nó lặp
        // lại y hệt ở đây: `ConfigureAppConfiguration` của `WebApplicationFactory` chỉ được áp
        // dụng SAU khi `builder.Configuration` đã bị đọc. Đọc sớm thì mọi phép ghi đè của bộ
        // kiểm thử bị bỏ qua trong im lặng — trần luôn rơi về giá trị mặc định, và hàng rào
        // trông như đang chạy trong khi nó chạy bằng một con số khác hẳn con số được yêu cầu.
        //
        // Tới lúc có request thì cấu hình đã dựng xong hoàn toàn. `GetFixedWindowLimiter` chỉ
        // gọi hàm dựng tùy chọn ở lần đầu của mỗi ngăn, nên phép đọc này không lặp lại mỗi lần.
        var settings = context.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetSection("Auth:LoginRateLimit");

        return RateLimitPartition.GetFixedWindowLimiter(
            // IP không đọc được thì gom chung vào một ngăn thay vì cho đi tự do: thà siết nhầm
            // còn hơn để một cấu hình proxy lạ vô hiệu hóa cả hàng rào.
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.GetValue("PermitLimit", 30),
                Window = TimeSpan.FromSeconds(settings.GetValue("WindowSeconds", 300)),
                // Không xếp hàng: request thừa bị từ chối ngay. Giữ chúng lại chờ tới lượt là
                // biến chính hàng rào này thành chỗ để làm nghẽn máy chủ.
                QueueLimit = 0
            });
    });

    // Trả đúng contract lỗi của dự án. Mặc định của bộ giới hạn là 429 với THÂN RỖNG, mà
    // `apiClient` ở frontend đọc `error.code` để quyết hiển thị gì — thân rỗng thì màn hình
    // báo "không đọc được phản hồi" thay vì nói cho người dùng biết họ cần chờ.
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ErrorResponse(new ErrorBody(
                ErrorCode.TooManyRequests,
                "Bạn đã thử đăng nhập quá nhiều lần. Vui lòng chờ vài phút rồi thử lại.",
                [])),
            cancellationToken);
    };
});

var app = builder.Build();

// Bộ xử lý lỗi phải đứng đầu chuỗi middleware để bắt được lỗi của mọi tầng phía sau.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Cố ý KHÔNG bật UseHttpsRedirection khi chạy dev: frontend gọi qua proxy của Vite trên
// http://localhost:5282. Bật chuyển hướng HTTPS sẽ khiến proxy nhận 307 và cookie
// Secure=false bị bỏ qua.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ── Giao diện dựng sẵn, nếu có ────────────────────────────────────────────────
// Khi wwwroot/ có một bản build của frontend, máy chủ này phục vụ luôn giao diện: một cổng,
// một lệnh, không cần Node lúc trình bày. Không có bản build thì khối này im lặng bỏ qua và
// máy chủ chạy đúng như cũ — chế độ phát triển vẫn là Vite ở cổng 3000 proxy sang đây.
//
// Đặt TRƯỚC UseRouting() và SessionMiddleware là có chủ ý: tệp tĩnh không thuộc về ai và
// không cần phiên đăng nhập. Cho chúng đi qua chuỗi kiểm tra quyền là bắt mỗi tấm ảnh phải
// tra một phiên trong database.
var hasFrontendBuild = app.Environment.WebRootPath is { Length: > 0 } webRoot
    && File.Exists(Path.Combine(webRoot, "index.html"));

if (hasFrontendBuild)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// ── Chuỗi kiểm tra quyền — BR-TENANT-013, thứ tự KHÔNG được đảo ───────────────
// UseRouting() được gọi tường minh để hai middleware bên dưới đọc được thông tin của
// endpoint sắp chạy; thiếu nó thì phép miễn trừ ở BR-TENANT-011 không tra được.
//
//   1. Tiệm còn hạn không?     → TenantWriteGuardMiddleware   (ngay dưới đây)
//   2. Gói có mở tính năng?    → RequirePermissionAttribute   (bộ lọc trên từng endpoint)
//   3. Vai trò có quyền?       → RequirePermissionAttribute   (ngay sau bước 2)
//   4. Dữ liệu thuộc tiệm nào? → bộ lọc toàn cục ở NailDbContext
app.UseRouting();

// Đứng NGAY SAU UseRouting và TRƯỚC SessionMiddleware, có chủ đích: chính sách gắn trên
// endpoint nên phải có kết quả định tuyến mới đọc được, còn chặn sớm thì một trận dò mật khẩu
// không kéo theo một lượt tra phiên trong database cho mỗi request rác.
app.UseRateLimiter();

// Đọc phiên ở MỖI request — BR-AUTH-022. Phải đứng trước mọi phép kiểm tra quyền, vì
// chúng đều hỏi "ai đang gọi" và "đang làm việc cho tiệm nào".
app.UseMiddleware<SessionMiddleware>();
app.UseMiddleware<TenantWriteGuardMiddleware>();

app.MapControllers();

// Hai mức kiểm tra sức khỏe, theo quy ước của bộ cân bằng tải và Kubernetes:
//   · /api/health        — SỐNG: tiến trình còn trả lời. Không chạm database, để database tạm
//                          mất không khiến nền tảng khởi động lại một tiến trình vẫn khỏe.
//   · /api/health/ready  — SẴN SÀNG: nối được database. Trả 503 khi không, để bộ cân bằng tải
//                          tạm rút bản này khỏi vòng quay thay vì đẩy request vào chỗ sẽ hỏng.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthCheckTags.Ready),
    ResponseWriter = HealthCheckResponse.WriteAsync
});

// Đường dẫn không khớp route nào. Cần khai báo riêng vì route không khớp KHÔNG đi qua
// ApiExceptionHandler — nếu bỏ qua thì client nhận 404 với thân rỗng, lệch contract lỗi
// và tầng service ở frontend không đọc được mã lỗi.
app.MapFallback("/api/{**path}", (HttpContext context) =>
{
    var body = new ErrorResponse(new ErrorBody(
        ErrorCode.NotFound,
        $"Không có endpoint {context.Request.Method} {context.Request.Path}.",
        []));

    return Results.Json(body, statusCode: StatusCodes.Status404NotFound);
});

// Mọi đường dẫn còn lại trả về index.html để React tự dựng màn hình.
//
// Frontend không có react-router: màn hình hiện tại là state trong App.tsx, không phải URL. Nên
// thực tế chỉ có đúng một đường "/" cần phục vụ — nhưng người dùng bấm F5 ở một URL gõ tay, hoặc
// mở lại một liên kết cũ, thì vẫn phải nhận giao diện chứ không phải 404 trắng.
//
// Khai báo SAU phần dự phòng của /api là bắt buộc về mặt ý, dù bộ định tuyến chọn theo độ cụ thể
// chứ không theo thứ tự: "/api/{**path}" hẹp hơn "{**path}" nên đường API vẫn nhận đúng JSON lỗi
// của mình. Nuốt mất nó thì một endpoint gõ sai sẽ trả về trang HTML, và tầng service ở frontend
// sẽ báo "không đọc được phản hồi" thay vì "không có endpoint này".
if (hasFrontendBuild)
{
    app.MapFallbackToFile("index.html");
}

// ── Khởi tạo database ─────────────────────────────────────────────────────────
// Áp migration, rồi mở một lối đăng nhập đầu tiên: dữ liệu demo ở máy phát triển, hoặc một tài
// khoản quản trị lấy từ cấu hình ở mọi nơi khác. Điều kiện nằm ở DemoSeedPolicy.
await app.BootstrapDatabaseAsync();

app.Run();

/// <summary>
/// Điểm vào của máy chủ, mở ra cho project kiểm thử tham chiếu tới.
/// <para>
/// Cần thiết vì <c>Program.cs</c> dùng lối viết lệnh ở cấp cao nhất, và trình biên dịch sinh
/// ra một lớp <c>Program</c> ở mức <c>internal</c>. <c>WebApplicationFactory&lt;TEntryPoint&gt;</c>
/// đòi một kiểu công khai để dựng lại đúng máy chủ này trong bộ nhớ — nhờ vậy bộ kiểm thử đi
/// qua trọn chuỗi middleware thật thay vì gọi thẳng vào use case.
/// </para>
/// </summary>
public partial class Program;
