using System.Net;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Authorization;

/// <summary>
/// BR-TENANT-010/011/012 — tiệm quá hạn hoặc bị khóa thì <b>xem được mọi thứ nhưng không ghi được
/// gì</b>, trừ hai nhóm miễn trừ.
/// <para>
/// Kiểm trên tiệm Muse chứ không phải Nailé: mọi phép thử khác đọc dữ liệu Nailé, và khóa nó lại
/// giữa chừng sẽ làm đỏ những phép thử không liên quan. Hạn dùng được trả về tương lai ngay sau
/// mỗi phép thử.
/// </para>
/// <para>
/// Trạng thái <c>OVERDUE</c> dựng bằng cách đẩy hạn dùng về quá khứ ở database, vì
/// <c>Tenant.Renew</c> cố ý từ chối mọi ngày quá khứ — xem <see cref="TestDatabase"/>. Đó cũng
/// chính là điều BR-TENANT-002 và BR-TENANT-003 nói: trạng thái này <b>tính lúc đọc</b> từ hạn
/// dùng, không ai đặt nó, và không có job nền nào chạy để đổi nó.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class ReadOnlyTenantTests(SalonSysFactory factory)
{
    private const string Muse = "TEN-MUSE";

    [Fact]
    public async Task Tiem_qua_han_van_doc_duoc_nhung_khong_ghi_duoc()
    {
        await TestDatabase.ExpireTenantAsync(factory, Muse);

        try
        {
            using var owner = await SalonSysClient.TenantAdminAsync(factory, Muse);

            var read = await owner.GetAsync("/api/customers");

            // BR-TENANT-012 — chế độ chỉ đọc không được chặn phần đọc. Chặn cả hai là biến một
            // tiệm chậm gia hạn thành một tiệm mất trắng dữ liệu trong mắt chính chủ của nó.
            Assert.Equal(HttpStatusCode.OK, read.Status);
            Assert.NotEmpty(read.ValuesOf("customers", "id"));

            var written = await owner.PostAsync("/api/customers", new { phone = "0900000002" });

            Assert.Equal(HttpStatusCode.Forbidden, written.Status);

            // Mã lỗi riêng chứ không dùng chung FORBIDDEN với thiếu quyền: frontend dựa vào nó để
            // hiện lời mời gia hạn thay vì một thông báo phân quyền vô nghĩa với người dùng.
            Assert.Equal("TENANT_READONLY", written.ErrorCode);
        }
        finally
        {
            await TestDatabase.RestoreTenantAsync(factory, Muse);
        }
    }

    /// <summary>
    /// BR-TENANT-011 — phép chặn phải phủ <b>mọi động từ ghi</b>, không riêng <c>POST</c>. Chặn
    /// mỗi <c>POST</c> là để ngỏ đúng những đường mà một tiệm hết hạn vẫn có thể dùng để tiếp tục
    /// vận hành: sửa hồ sơ, đổi trạng thái, dời lịch.
    /// </summary>
    [Fact]
    public async Task Tiem_bi_khoa_chan_moi_dong_tu_ghi()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);
        await superadmin.PatchAsync($"/api/tenants/{Muse}/status", new { status = "SUSPENDED" });

        try
        {
            using var owner = await SalonSysClient.TenantAdminAsync(factory, Muse);
            var customerId = (await owner.GetAsync("/api/customers")).ValuesOf("customers", "id")[0];

            var created = await owner.PostAsync("/api/customers", new { phone = "0900000003" });
            var replaced = await owner.PutAsync($"/api/customers/{customerId}", new { phone = "0900000004" });
            var patched = await owner.PatchAsync(
                $"/api/customers/{customerId}/status", new { status = "INACTIVE" });

            Assert.All(
                [created, replaced, patched],
                response =>
                {
                    Assert.Equal(HttpStatusCode.Forbidden, response.Status);
                    Assert.Equal("TENANT_READONLY", response.ErrorCode);
                });
        }
        finally
        {
            await superadmin.PatchAsync($"/api/tenants/{Muse}/status", new { status = "ACTIVE" });
        }
    }

    /// <summary>
    /// BR-TENANT-011 — phép miễn trừ. Ba lệnh của tầng xác thực mang
    /// <c>AllowWhenTenantReadonly</c>, và cả ba đều là <c>POST</c> nên chúng sẽ bị lệnh chặn ghi
    /// nuốt mất nếu thiếu dấu miễn trừ ấy.
    /// <para>
    /// Hậu quả nếu để sót: chủ tiệm hết hạn không <b>chọn được tiệm</b> của mình, nên họ không
    /// vào nổi màn hình để đọc lý do bị khóa — và cũng không đăng xuất được. Hệ thống tự nhốt
    /// người dùng ở ngoài cửa đúng lúc họ cần vào nhất.
    /// </para>
    /// <para>
    /// Hai nhóm miễn trừ mà BR-TENANT-011 nêu — yêu cầu nâng cấp gói và nộp chứng từ hóa đơn đăng
    /// ký — chưa có endpoint nào, vì lát cắt gói đăng ký đã bị cắt khỏi lộ trình (§0 mục 13). Khi
    /// nào chúng có thì phép thử này là chỗ để thêm vào.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Tiem_bi_khoa_van_dang_nhap_chon_tiem_va_dang_xuat_duoc()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);
        await superadmin.PatchAsync($"/api/tenants/{Muse}/status", new { status = "SUSPENDED" });

        try
        {
            using var owner = SalonSysClient.Anonymous(factory);

            var login = await owner.LoginAsync(
                SalonSysClient.TenantAdminEmail, SalonSysClient.TenantAdminPassword);
            var chosen = await owner.SelectTenantAsync(Muse);

            Assert.Equal(HttpStatusCode.OK, login.Status);
            Assert.Equal(HttpStatusCode.OK, chosen.Status);

            // Và tiệm tự khai đúng tình trạng của mình, để giao diện biết mà hiện lời mời gia hạn.
            Assert.True(chosen.Body.GetProperty("tenant").GetProperty("isReadOnly").GetBoolean());

            // Đăng xuất trả 204 vì nó không có gì để nói ngoài "xong".
            Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync("/api/auth/logout")).Status);
        }
        finally
        {
            await superadmin.PatchAsync($"/api/tenants/{Muse}/status", new { status = "ACTIVE" });
        }
    }
}
