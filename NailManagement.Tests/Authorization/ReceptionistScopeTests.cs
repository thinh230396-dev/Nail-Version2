using System.Net;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Authorization;

/// <summary>
/// BR-ISO-004 — phạm vi dữ liệu của lễ tân đi theo <b>hai luật khác nhau</b>, và đó chính là chỗ
/// dễ nhầm nhất trong cả bảng phân quyền:
/// <list type="bullet">
/// <item>Lịch hẹn, nhân viên, hóa đơn bán hàng — <b>chỉ chi nhánh mình</b>.</item>
/// <item>Khách hàng và dịch vụ — <b>toàn tiệm</b>.</item>
/// </list>
/// <para>
/// Ngoại lệ về khách hàng không phải là sơ suất: BR-CUS-001 cho khách thuộc tiệm chứ không thuộc
/// chi nhánh. Lọc khách theo chi nhánh nghĩa là lễ tân Quận 3 không tra được một khách vừa đến
/// Quận 1 tuần trước, rồi lập cho họ một hồ sơ trùng — đúng thứ mà ràng buộc số điện thoại duy
/// nhất ở BR-CUS-002 đang đi ngăn.
/// </para>
/// <para>
/// Tài khoản lễ tân mẫu thuộc chi nhánh Quận 3 của Nailé, gắn qua hồ sơ nhân viên theo BR-EMP-004.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class ReceptionistScopeTests(SalonSysFactory factory)
{
    private const string OwnBranch = "BRN-LUMIERE-Q3";
    private const string OtherBranch = "BRN-LUMIERE-Q1";

    private static string Range =>
        $"?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-45).ToString("O"))}"
        + $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(2).ToString("O"))}";

    [Theory]
    [InlineData("/api/staff", "staff")]
    [InlineData("/api/appointments", "appointments")]
    [InlineData("/api/sales-invoices", "invoices")]
    public async Task Chi_thay_ban_ghi_cua_chi_nhanh_minh(string path, string arrayProperty)
    {
        var query = path == "/api/staff" ? string.Empty : Range;

        using var receptionist = await SalonSysClient.ReceptionistAsync(factory);
        var response = await receptionist.GetAsync(path + query);

        Assert.Equal(HttpStatusCode.OK, response.Status);

        var branches = response.ValuesOf(arrayProperty, "branchId").Distinct().ToList();

        Assert.NotEmpty(branches);
        Assert.Equal([OwnBranch], branches);
    }

    /// <summary>
    /// Ngoại lệ của bảng BR-ISO-004: lễ tân thấy <b>đúng cùng một danh bạ khách</b> với chủ tiệm.
    /// So bằng tập mã định danh chứ không bằng số lượng, để một phép lọc sai mà tình cờ cho ra
    /// cùng số dòng vẫn bị bắt.
    /// </summary>
    [Fact]
    public async Task Thay_toan_bo_khach_hang_cua_tiem_giong_het_chu_tiem()
    {
        using var receptionist = await SalonSysClient.ReceptionistAsync(factory);
        using var tenantAdmin = await SalonSysClient.TenantAdminAsync(factory, "TEN-LUMIERE");

        var seenByReception = await receptionist.GetAsync("/api/customers");
        var seenByOwner = await tenantAdmin.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.OK, seenByReception.Status);
        Assert.NotEmpty(seenByReception.ValuesOf("customers", "id"));

        Assert.Equal(
            seenByOwner.ValuesOf("customers", "id").Order(),
            seenByReception.ValuesOf("customers", "id").Order());
    }

    /// <summary>
    /// Phạm vi phải chặn cả đường <b>ghi</b>, không chỉ đường đọc. Lọc danh sách mà quên chặn lệnh
    /// sửa thì lễ tân vẫn thao tác được lên bản ghi chi nhánh khác — họ chỉ cần biết mã định danh.
    /// </summary>
    [Fact]
    public async Task Khong_thao_tac_duoc_len_ban_ghi_cua_chi_nhanh_khac()
    {
        using var tenantAdmin = await SalonSysClient.TenantAdminAsync(factory, "TEN-LUMIERE");
        var all = await tenantAdmin.GetAsync("/api/appointments" + Range);

        var otherBranchAppointment = all.Body.GetProperty("appointments").EnumerateArray()
            .First(item => item.GetProperty("branchId").GetString() == OtherBranch)
            .GetProperty("id").GetString();

        using var receptionist = await SalonSysClient.ReceptionistAsync(factory);

        var read = await receptionist.GetAsync($"/api/appointments/{otherBranchAppointment}");
        var written = await receptionist.PatchAsync(
            $"/api/appointments/{otherBranchAppointment}/status", new { status = "CANCELLED" });

        Assert.Equal(HttpStatusCode.NotFound, read.Status);
        Assert.Equal(HttpStatusCode.NotFound, written.Status);
    }

    /// <summary>
    /// Ba ô cố ý vắng mặt với lễ tân trong ma trận mục 3.4. Vắng mặt nghĩa là không có quyền, và
    /// phép thử này giữ cho điều đó vẫn đúng sau mỗi lần ai đó thêm một dòng vào bảng.
    /// </summary>
    [Theory]
    [InlineData("/api/tenants")]
    [InlineData("/api/packages")]
    public async Task Khong_cham_duoc_vao_tang_nen_tang(string path)
    {
        using var receptionist = await SalonSysClient.ReceptionistAsync(factory);

        var response = await receptionist.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
    }
}
