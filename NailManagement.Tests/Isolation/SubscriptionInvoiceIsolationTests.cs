using System.Net;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Isolation;

/// <summary>
/// Cách ly sổ hóa đơn đăng ký — BR-INV-032 và BR-ISO-006.
///
/// <para>
/// Tách khỏi <see cref="TenantIsolationTests"/> dù cùng nói về cách ly, vì bảng này là ngoại lệ
/// của mọi bảng khác: nó KHÔNG mang bộ lọc theo tiệm ở tầng dữ liệu (BR-TENANT-022 — hóa đơn của
/// tiệm đã xóa mềm vẫn phải đọc được để tính doanh thu nền tảng). Phạm vi của nó vì thế do use
/// case tự thu hẹp, và một lớp bảo vệ đứng một mình thì phải có test đứng một mình.
/// </para>
/// <para>
/// Lỗ hổng mà bộ test này đi giữ là thật, không phải giả định: trước ngày 24, ma trận quyền cấp
/// cho chủ tiệm ô <c>SubscriptionInvoices</c> với ghi chú "phạm vi do bộ lọc dữ liệu lo" — trong
/// khi bộ lọc ấy chưa từng tồn tại cho bảng này. Chủ tiệm gọi thẳng endpoint là đọc được hóa đơn
/// của mọi tiệm, kèm tên tiệm và số tiền.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class SubscriptionInvoiceIsolationTests(SalonSysFactory factory)
{
    private const string Lumiere = "TEN-LUMIERE";
    private const string Muse = "TEN-MUSE";

    private const string Path = "/api/subscription-invoices";

    [Fact]
    public async Task Chu_tiem_chi_doc_duoc_hoa_don_dang_ky_cua_tiem_dang_lam_viec()
    {
        using var atLumiere = await SalonSysClient.TenantAdminAsync(factory, Lumiere);
        var mine = await atLumiere.GetAsync(Path);

        Assert.Equal(HttpStatusCode.OK, mine.Status);

        var tenants = mine.ValuesOf("invoices", "tenantId").ToHashSet();

        // Có dòng để đọc, nếu không thì phép khẳng định dưới đây xanh một cách vô nghĩa.
        Assert.NotEmpty(tenants);

        // Và mọi dòng đều thuộc đúng tiệm đang làm việc — không một mã tiệm nào khác lọt vào.
        Assert.Equal([Lumiere], tenants);
    }

    [Fact]
    public async Task Doi_tiem_thi_so_hoa_don_doi_theo()
    {
        using var atLumiere = await SalonSysClient.TenantAdminAsync(factory, Lumiere);
        var lumiere = await atLumiere.GetAsync(Path);

        using var atMuse = await SalonSysClient.TenantAdminAsync(factory, Muse);
        var muse = await atMuse.GetAsync(Path);

        var lumiereIds = lumiere.ValuesOf("invoices", "id").ToHashSet();
        var museIds = muse.ValuesOf("invoices", "id").ToHashSet();

        Assert.NotEmpty(lumiereIds);
        Assert.NotEmpty(museIds);

        // Cùng một tài khoản chủ tiệm, chỉ khác tiệm đang chọn (BR-AUTH-023). Không một hóa đơn
        // nào được xuất hiện ở cả hai lần đọc.
        Assert.Empty(lumiereIds.Intersect(museIds));
    }

    [Fact]
    public async Task Superadmin_van_doc_duoc_ca_so_cua_moi_tiem()
    {
        using var asSuperAdmin = await SalonSysClient.SuperAdminAsync(factory);
        var all = await asSuperAdmin.GetAsync(Path);

        Assert.Equal(HttpStatusCode.OK, all.Status);

        var tenants = all.ValuesOf("invoices", "tenantId").ToHashSet();

        // BR-REV-008: doanh thu nền tảng cộng từ chính bảng này, nên thu hẹp phạm vi cho chủ
        // tiệm không được phép làm hẹp luôn tầm nhìn của Superadmin.
        Assert.True(
            tenants.Count > 1,
            "Superadmin phải thấy hóa đơn của nhiều hơn một tiệm, nếu không thì doanh thu nền tảng đã bị cắt xén.");

        Assert.Contains(Lumiere, tenants);
        Assert.Contains(Muse, tenants);
    }

    [Fact]
    public async Task Le_tan_khong_doc_duoc_so_hoa_don_dang_ky()
    {
        using var atReception = await SalonSysClient.ReceptionistAsync(factory);
        var refused = await atReception.GetAsync(Path);

        // Tiền tiệm trả cho SalonSys không phải việc của quầy — ô này cố ý vắng mặt trong ma
        // trận mục 3.4, và vắng mặt nghĩa là không có quyền.
        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
    }
}
