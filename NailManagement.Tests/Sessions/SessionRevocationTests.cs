using System.Net;
using System.Text.Json;
using NailManagement.Tests.Infrastructure;

using static NailManagement.Tests.Scenarios.SalonScenario;

namespace NailManagement.Tests.Sessions;

/// <summary>
/// Quản trị phiên đăng nhập — BR-AUTH-032, BR-AUTH-033.
/// <para>
/// Phép thử đáng giá nhất ở đây là <see cref="Thu_hoi_xong_thi_nguoi_kia_bi_da_ra_ngay"/>: nó
/// chứng minh việc thu hồi thật sự có hiệu lực chứ không chỉ đổi một cột. Nếu
/// <c>SessionMiddleware</c> ngừng đọc lại phiên ở mỗi request thì phép thử ấy đỏ, và đó đúng
/// là thứ BR-AUTH-022 hứa.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class SessionRevocationTests(SalonSysFactory factory)
{
    [Fact]
    public async Task Superadmin_thay_phien_cua_moi_tiem()
    {
        // Ba phiên của ba vai khác nhau, mở trước khi đọc danh sách.
        using var lumiere = await SalonSysClient.TenantAdminAsync(factory, Lumiere);
        using var reception = await SalonSysClient.ReceptionistAsync(factory);
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        var response = await superadmin.GetAsync("/api/sessions");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(response.CountOf("sessions") >= 3);
    }

    [Fact]
    public async Task Chu_tiem_chi_thay_phien_cua_nguoi_trong_tiem_minh()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);
        using var lumiere = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var response = await lumiere.GetAsync("/api/sessions");

        Assert.Equal(HttpStatusCode.OK, response.Status);

        // Phiên của Superadmin vừa mở ở trên KHÔNG được lọt vào danh sách của chủ tiệm:
        // tài khoản đó không có liên kết nào tới tiệm này trong bảng UserTenants.
        var owners = Sessions(response).Select(session => Text(session, "userId")).ToArray();

        Assert.NotEmpty(owners);
        Assert.DoesNotContain("USR-SUPERADMIN", owners);
    }

    [Fact]
    public async Task Le_tan_khong_xem_duoc_danh_sach_phien()
    {
        using var reception = await SalonSysClient.ReceptionistAsync(factory);

        var response = await reception.GetAsync("/api/sessions");

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Equal("FORBIDDEN", response.ErrorCode);
    }

    [Fact]
    public async Task Khong_tu_thu_hoi_duoc_phien_minh_dang_dung()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        var listed = await superadmin.GetAsync("/api/sessions");
        var mine = Sessions(listed).Single(session => session.GetProperty("isCurrent").GetBoolean());

        var response = await superadmin.PostAsync($"/api/sessions/{Text(mine, "id")}/revoke", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Equal("FORBIDDEN", response.ErrorCode);
    }

    [Fact]
    public async Task Chu_tiem_khong_thu_hoi_duoc_phien_ngoai_tiem_minh()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);
        using var lumiere = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        // Superadmin tự tra mã phiên của chính mình rồi đưa cho chủ tiệm thử đóng.
        var listed = await superadmin.GetAsync("/api/sessions");
        var superadminSession = Sessions(listed)
            .Single(session => session.GetProperty("isCurrent").GetBoolean());

        var response = await lumiere.PostAsync(
            $"/api/sessions/{Text(superadminSession, "id")}/revoke", new { });

        // 404 chứ không phải 403 — trả 403 là xác nhận phiên đó có thật, tức là rò rỉ một mẩu
        // thông tin về tiệm khác. Cùng lối mà BR-ISO-003 đặt cho mọi tài nguyên xuyên tiệm.
        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }

    [Fact]
    public async Task Thu_hoi_xong_thi_nguoi_kia_bi_da_ra_ngay()
    {
        using var victim = await SalonSysClient.ReceptionistAsync(factory);
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        // Trước khi bị thu hồi, lễ tân làm việc bình thường.
        Assert.Equal(HttpStatusCode.OK, (await victim.GetAsync("/api/appointments")).Status);

        var listed = await superadmin.GetAsync("/api/sessions");
        var victimSession = Sessions(listed).First(session =>
            Text(session, "userRole") == "RECEPTIONIST"
            && Text(session, "status") == "ACTIVE");

        var revoked = await superadmin.PostAsync(
            $"/api/sessions/{Text(victimSession, "id")}/revoke", new { });

        Assert.Equal(HttpStatusCode.OK, revoked.Status);
        Assert.Equal("REVOKED", Text(revoked.Body.GetProperty("session"), "status"));

        // Đây là điều thật sự cần chứng minh: BR-AUTH-022 nói phiên bị vô hiệu ngay ở request
        // kế tiếp, và đây là request kế tiếp.
        var after = await victim.GetAsync("/api/appointments");

        Assert.Equal(HttpStatusCode.Unauthorized, after.Status);
    }

    [Fact]
    public async Task Thu_hoi_hai_lan_khong_phai_loi()
    {
        using var victim = await SalonSysClient.TenantAdminAsync(factory, Lumiere);
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        var listed = await superadmin.GetAsync("/api/sessions");
        var target = Sessions(listed).First(session =>
            Text(session, "userRole") == "TENANT_ADMIN"
            && Text(session, "status") == "ACTIVE");

        var first = await superadmin.PostAsync($"/api/sessions/{Text(target, "id")}/revoke", new { });
        var second = await superadmin.PostAsync($"/api/sessions/{Text(target, "id")}/revoke", new { });

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal("REVOKED", Text(second.Body.GetProperty("session"), "status"));
    }

    /// <summary>
    /// BR-AUD-002 — thu hồi phiên để lại vết, và bằng sự kiện của riêng nó.
    /// <para>
    /// Không dùng lại <c>ACCOUNT_SUSPENDED</c> dù cả hai đều là "đá một người ra ngoài": khóa
    /// tài khoản chặn người đó đăng nhập lại, còn thu hồi phiên chỉ đóng đúng một thiết bị và
    /// họ vào lại được ngay. Trộn hai thứ thì mọi dòng trong sổ đều đọc như biện pháp nặng.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Thu_hoi_phien_de_lai_vet_trong_nhat_ky()
    {
        using var victim = await SalonSysClient.ReceptionistAsync(factory);
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        var listed = await superadmin.GetAsync("/api/sessions");
        var target = Sessions(listed).First(session =>
            Text(session, "userRole") == "RECEPTIONIST"
            && Text(session, "status") == "ACTIVE");

        await superadmin.PostAsync($"/api/sessions/{Text(target, "id")}/revoke", new { });

        var logs = await superadmin.GetAsync("/api/audit-logs?take=50");

        Assert.Contains("SESSION_REVOKED", logs.ValuesOf("entries", "event"));

        // Bản ghi phải nói được AI làm, bằng tên người chứ không phải mã tài khoản. Bảng nhật ký
        // vẫn chỉ lưu mã (BR-AUD-003); tên do use case tra lúc đọc và gửi kèm.
        var names = logs.ValuesOf("entries", "actorDisplayName");

        Assert.Contains(names, name => !string.IsNullOrWhiteSpace(name));
        Assert.DoesNotContain(names, name => name is not null && name.StartsWith("USR-"));
    }

    private static IEnumerable<JsonElement> Sessions(ApiResponse response)
        => response.Body.GetProperty("sessions").EnumerateArray();
}
