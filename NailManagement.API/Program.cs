using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NailManagement.API.Common;
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
