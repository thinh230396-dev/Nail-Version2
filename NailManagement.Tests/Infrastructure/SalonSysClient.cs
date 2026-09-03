using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace NailManagement.Tests.Infrastructure;

/// <summary>Kết quả một lời gọi API, đủ để phép thử khẳng định cả mã trạng thái lẫn thân trả về.</summary>
public sealed record ApiResponse(HttpStatusCode Status, JsonElement Body)
{
    /// <summary>Mã lỗi nghiệp vụ trong contract <c>{ error: { code, message, fields } }</c>, hoặc rỗng nếu không phải lỗi.</summary>
    public string? ErrorCode => Body.ValueKind == JsonValueKind.Object
                                && Body.TryGetProperty("error", out var error)
                                && error.TryGetProperty("code", out var code)
        ? code.GetString()
        : null;

    /// <summary>Số phần tử của một mảng nằm dưới khóa cho trước, ví dụ <c>customers</c> hay <c>appointments</c>.</summary>
    public int CountOf(string arrayProperty) => Body.GetProperty(arrayProperty).GetArrayLength();

    /// <summary>Các giá trị của một trường trên mọi phần tử của mảng — dùng để kiểm phạm vi dữ liệu.</summary>
    public IReadOnlyList<string?> ValuesOf(string arrayProperty, string field)
        => [.. Body.GetProperty(arrayProperty).EnumerateArray()
            .Select(item => item.TryGetProperty(field, out var value) ? value.GetString() : null)];
}

/// <summary>
/// Một phiên đăng nhập nói chuyện với máy chủ dựng trong bộ nhớ.
/// <para>
/// Giữ cookie giữa các lời gọi bằng <see cref="CookieContainerHandler"/>, đúng cách trình duyệt
/// làm: quyết định 11 chốt phiên là cookie kèm bảng <c>app_sessions</c> chứ không phải JWT, nên
/// một bộ kiểm thử tự gắn header sẽ đi vòng qua chính thứ cần kiểm.
/// </para>
/// </summary>
public sealed class SalonSysClient(HttpClient http) : IDisposable
{
    /// <summary>Ba tài khoản mà <c>DemoAccountSeeder</c> nạp sẵn, dùng chung cho mọi phép thử.</summary>
    public const string SuperAdminEmail = "superadmin@salonsys.vn";

    public const string SuperAdminPassword = "Super@2026";
    public const string TenantAdminEmail = "tenantadmin@lumierehair.vn";
    public const string TenantAdminPassword = "Lumiere@2026";
    public const string ReceptionistEmail = "receptionist@nailestudio.vn";
    public const string ReceptionistPassword = "Reception@2026";

    /// <summary>Đăng nhập rồi chọn tiệm trong một bước — đường đi thường gặp nhất của chủ tiệm.</summary>
    public static async Task<SalonSysClient> TenantAdminAsync(SalonSysFactory factory, string tenantId)
    {
        var client = Anonymous(factory);

        await client.LoginAsync(TenantAdminEmail, TenantAdminPassword);
        await client.SelectTenantAsync(tenantId);

        return client;
    }

    public static async Task<SalonSysClient> SuperAdminAsync(SalonSysFactory factory)
    {
        var client = Anonymous(factory);

        await client.LoginAsync(SuperAdminEmail, SuperAdminPassword);

        return client;
    }

    /// <summary>
    /// Lễ tân <b>không chọn tiệm</b>: BR-AUTH-024 gắn họ vào đúng một tiệm qua hồ sơ nhân viên,
    /// nên phiên của họ đã có sẵn phạm vi ngay sau khi đăng nhập.
    /// </summary>
    public static async Task<SalonSysClient> ReceptionistAsync(SalonSysFactory factory)
    {
        var client = Anonymous(factory);

        await client.LoginAsync(ReceptionistEmail, ReceptionistPassword);

        return client;
    }

    public static SalonSysClient Anonymous(SalonSysFactory factory)
        => new(factory.CreateDefaultClient(new CookieContainerHandler()));

    public Task<ApiResponse> LoginAsync(string identifier, string password)
        => PostAsync("/api/auth/login", new { identifier, password });

    public Task<ApiResponse> SelectTenantAsync(string tenantId)
        => PostAsync("/api/auth/session/tenant", new { tenantId });

    public Task<ApiResponse> GetAsync(string path) => SendAsync(HttpMethod.Get, path);

    public Task<ApiResponse> PostAsync(string path, object? body = null)
        => SendAsync(HttpMethod.Post, path, body);

    public Task<ApiResponse> PutAsync(string path, object? body = null)
        => SendAsync(HttpMethod.Put, path, body);

    public Task<ApiResponse> PatchAsync(string path, object? body = null)
        => SendAsync(HttpMethod.Patch, path, body);

    public Task<ApiResponse> DeleteAsync(string path) => SendAsync(HttpMethod.Delete, path);

    public void Dispose() => http.Dispose();

    private async Task<ApiResponse> SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null) request.Content = JsonContent.Create(body);

        using var response = await http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        // Thân rỗng vẫn phải trả về một JsonElement hợp lệ, để phép thử chỉ quan tâm mã trạng
        // thái không phải tự phòng thủ trước giá trị rỗng.
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);

        return new ApiResponse(response.StatusCode, document.RootElement.Clone());
    }
}
