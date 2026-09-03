using System.Net;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Isolation;

/// <summary>
/// BR-ISO-006 — cách ly tiệm là <b>hạng mục kiểm thử bắt buộc</b>, và §6 của lộ trình xếp nó vào
/// bốn thứ tuyệt đối không cắt.
/// <para>
/// Chỗ dễ hở nhất của hệ thống này là nó cho <b>một tài khoản quản nhiều tiệm</b> (BR-AUTH-023):
/// cùng một người đăng nhập, chỉ khác tiệm đang chọn. Nếu phạm vi tiệm sống ở sai chỗ — trong
/// một biến tĩnh, hay bị nhớ lại giữa các request — thì lỗi lộ ra đúng ở đây chứ không ở đâu khác.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class TenantIsolationTests(SalonSysFactory factory)
{
    private const string Lumiere = "TEN-LUMIERE";
    private const string Muse = "TEN-MUSE";

    /// <summary>Khoảng đủ rộng để phủ trọn ba mươi ngày dữ liệu mẫu, và vẫn dưới trần 92 ngày.</summary>
    private static string Range =>
        $"?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-45).ToString("O"))}"
        + $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(2).ToString("O"))}";

    [Theory]
    [InlineData("customers", "/api/customers", "customers")]
    [InlineData("appointments", "/api/appointments", "appointments")]
    [InlineData("sales-invoices", "/api/sales-invoices", "invoices")]
    public async Task Ho_so_cua_tiem_khac_tra_ve_404_chu_khong_phai_403(
        string module, string path, string arrayProperty)
    {
        var query = module == "customers" ? string.Empty : Range;

        using var atLumiere = await SalonSysClient.TenantAdminAsync(factory, Lumiere);
        var mine = await atLumiere.GetAsync(path + query);

        Assert.Equal(HttpStatusCode.OK, mine.Status);
        Assert.True(mine.CountOf(arrayProperty) > 0, $"Dữ liệu mẫu phải có ít nhất một {module} ở Nailé.");

        var borrowedId = mine.Body.GetProperty(arrayProperty)[0].GetProperty("id").GetString();

        using var atMuse = await SalonSysClient.TenantAdminAsync(factory, Muse);
        var stolen = await atMuse.GetAsync($"{path}/{borrowedId}");

        // 404 chứ không phải 403, theo BR-TENANT-013 bước 4: trả 403 là vô tình xác nhận
        // "bản ghi đó có tồn tại, chỉ là bạn không được xem", và ghép nhiều câu trả lời như vậy
        // lại là đoán được dữ liệu của tiệm khác.
        Assert.Equal(HttpStatusCode.NotFound, stolen.Status);
        Assert.Equal("NOT_FOUND", stolen.ErrorCode);
    }

    [Fact]
    public async Task Hai_tiem_cua_cung_mot_chu_khong_thay_du_lieu_cua_nhau()
    {
        using var atLumiere = await SalonSysClient.TenantAdminAsync(factory, Lumiere);
        var lumiereCustomers = await atLumiere.GetAsync("/api/customers");

        using var atMuse = await SalonSysClient.TenantAdminAsync(factory, Muse);
        var museCustomers = await atMuse.GetAsync("/api/customers");

        var lumiereIds = lumiereCustomers.ValuesOf("customers", "id").ToHashSet();
        var museIds = museCustomers.ValuesOf("customers", "id").ToHashSet();

        Assert.NotEmpty(lumiereIds);
        Assert.NotEmpty(museIds);

        // Không một hồ sơ nào xuất hiện ở cả hai danh sách. Đây là phép khẳng định mạnh hơn hẳn
        // "đếm số dòng khác nhau": hai tiệm có thể tình cờ cùng số khách.
        Assert.Empty(lumiereIds.Intersect(museIds));
    }

    [Fact]
    public async Task Doi_tiem_trong_cung_mot_phien_nap_lai_dung_du_lieu_moi()
    {
        using var client = SalonSysClient.Anonymous(factory);

        await client.LoginAsync(SalonSysClient.TenantAdminEmail, SalonSysClient.TenantAdminPassword);

        await client.SelectTenantAsync(Lumiere);
        var atLumiere = await client.GetAsync("/api/customers");

        await client.SelectTenantAsync(Muse);
        var atMuse = await client.GetAsync("/api/customers");

        // Cùng một phiên, cùng một cookie, chỉ khác tiệm đang chọn. Nếu phạm vi tiệm bị nhớ lại
        // giữa hai request thì lần thứ hai vẫn trả về danh sách của Nailé — đúng kiểu lỗi mà
        // rủi ro số 2 ở §7 của lộ trình cảnh báo.
        Assert.Empty(atLumiere.ValuesOf("customers", "id").ToHashSet()
            .Intersect(atMuse.ValuesOf("customers", "id").ToHashSet()));
    }

    /// <summary>
    /// BR-AUTH-026 — tài khoản chỉ chọn được tiệm có trong <c>user_tenants</c>. Đây là bước 2 của
    /// trình tự bắt buộc ở BR-ISO-003, và bỏ nó là mở đường truy cập chéo tiệm.
    /// </summary>
    [Theory]
    [InlineData("TEN-BLOOM")]
    [InlineData("TEN-OASIS")]
    [InlineData("TEN-KHONG-CO-THAT")]
    public async Task Khong_chon_duoc_tiem_khong_duoc_giao_quan_ly(string tenantId)
    {
        using var client = SalonSysClient.Anonymous(factory);

        await client.LoginAsync(SalonSysClient.TenantAdminEmail, SalonSysClient.TenantAdminPassword);

        var chosen = await client.SelectTenantAsync(tenantId);

        Assert.Equal(HttpStatusCode.Forbidden, chosen.Status);
        Assert.Equal("FORBIDDEN", chosen.ErrorCode);

        // Và phiên vẫn chưa có tiệm nào, chứ không phải bị gán nhầm sang tiệm vừa bị từ chối.
        var customers = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Forbidden, customers.Status);
    }
}
