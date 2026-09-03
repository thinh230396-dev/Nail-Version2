using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NailManagement.API.Common;
using NailManagement.API.Security;
using NailManagement.Application;
using NailManagement.Domain.Common;
using NailManagement.Infrastructure;
using NailManagement.Infrastructure.Persistence;
using NailManagement.Infrastructure.Persistence.Seed;

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

// Đọc phiên ở MỖI request — BR-AUTH-022. Phải đứng trước mọi phép kiểm tra quyền, vì
// chúng đều hỏi "ai đang gọi" và "đang làm việc cho tiệm nào".
app.UseMiddleware<SessionMiddleware>();
app.UseMiddleware<TenantWriteGuardMiddleware>();

app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

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
// Áp migration rồi nạp tài khoản demo, để lần chạy đầu trên máy sạch không cần thao tác tay.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NailDbContext>();
    await db.Database.MigrateAsync();

    var accountSeeder = scope.ServiceProvider.GetRequiredService<DemoAccountSeeder>();
    var seededAccounts = await accountSeeder.SeedAsync();
    if (seededAccounts > 0)
    {
        app.Logger.LogInformation("Đã nạp {Count} tài khoản demo.", seededAccounts);
    }

    // Dữ liệu nghiệp vụ phải nạp SAU tài khoản: bộ nạp này gắn tài khoản lễ tân có sẵn vào
    // hồ sơ nhân viên vừa tạo (BR-AUTH-013), nên nó cần ba tài khoản kia đã nằm trong database.
    var dataSeeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    if (await dataSeeder.SeedAsync())
    {
        app.Logger.LogInformation("Đã nạp dữ liệu mẫu: gói dịch vụ, tiệm, nhân viên, khách hàng, lịch hẹn và hóa đơn.");
    }
}

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
