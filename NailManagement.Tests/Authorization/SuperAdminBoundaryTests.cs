using System.Net;
using NailManagement.Domain.Access;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Authorization;

/// <summary>
/// BR-AUTH-030 và BR-ISO-005 — Superadmin quản nền tảng, <b>không chạm được vào dữ liệu nghiệp vụ
/// bên trong một tiệm</b>. Ranh giới này phải cưỡng chế ở tầng API chứ không chỉ ở giao diện.
/// <para>
/// Vì sao đáng kiểm riêng: đây là ranh giới ngược chiều trực giác. Ở phần lớn hệ thống, "quản trị
/// hệ thống" là vai có nhiều quyền nhất, nên một lập trình viên vô tình thêm ô Superadmin vào ma
/// trận sẽ thấy nó hợp lý. Hai lớp chặn ở đây là bảng <c>PermissionMatrix</c> và bộ lọc theo tiệm
/// ở <c>NailDbContext</c>, và phép thử này kiểm lớp thứ nhất.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class SuperAdminBoundaryTests(SalonSysFactory factory)
{
    [Theory]
    [InlineData("/api/customers")]
    [InlineData("/api/appointments")]
    [InlineData("/api/sales-invoices")]
    [InlineData("/api/staff")]
    public async Task Superadmin_khong_doc_duoc_du_lieu_nghiep_vu_trong_tiem(string path)
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        var response = await superadmin.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Equal("FORBIDDEN", response.ErrorCode);
    }

    [Fact]
    public async Task Superadmin_khong_ghi_duoc_du_lieu_nghiep_vu_trong_tiem()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        var createdCustomer = await superadmin.PostAsync("/api/customers", new { phone = "0900000001" });
        var createdInvoice = await superadmin.PostAsync("/api/sales-invoices", new { customerId = "x" });

        Assert.Equal(HttpStatusCode.Forbidden, createdCustomer.Status);
        Assert.Equal(HttpStatusCode.Forbidden, createdInvoice.Status);
    }

    /// <summary>
    /// Mặt còn lại của cùng một ranh giới: Superadmin <b>vẫn</b> làm được việc của mình. Thiếu
    /// phép thử này thì một lần siết quyền quá tay sẽ khóa luôn cả cổng quản trị mà không ai biết.
    /// </summary>
    [Fact]
    public async Task Superadmin_van_quan_duoc_tang_nen_tang()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        var tenants = await superadmin.GetAsync("/api/tenants");
        var packages = await superadmin.GetAsync("/api/packages");

        Assert.Equal(HttpStatusCode.OK, tenants.Status);
        Assert.Equal(HttpStatusCode.OK, packages.Status);
        Assert.True(tenants.CountOf("tenants") >= 2);
    }

    /// <summary>
    /// Chiều ngược lại của ma trận: chủ tiệm không quản được tiệm của người khác.
    /// <para>
    /// Ghép chung lớp này vì đó là cùng một câu hỏi — "ai được chạm vào cái gì" — chỉ khác vai.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Chu_tiem_khong_quan_duoc_tang_nen_tang()
    {
        using var tenantAdmin = await SalonSysClient.TenantAdminAsync(factory, "TEN-LUMIERE");

        var tenants = await tenantAdmin.GetAsync("/api/tenants");
        var packages = await tenantAdmin.GetAsync("/api/packages");

        Assert.Equal(HttpStatusCode.Forbidden, tenants.Status);
        Assert.Equal(HttpStatusCode.Forbidden, packages.Status);
    }
}
