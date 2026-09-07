using System.Net;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Accounts;

/// <summary>
/// Khóa và mở khóa tài khoản chủ tiệm — BR-AUTH-020, BR-AUTH-021, BR-AUTH-022.
/// <para>
/// Mọi phép thử ở đây thao tác trên tài khoản chủ tiệm của <b>Bloom</b>, không phải tài khoản
/// dùng chung của Nailé. Lý do giống hệt lý do <c>ReadOnlyTenantTests</c> chọn tiệm Muse: tài
/// khoản Nailé là tài khoản mà gần như mọi lớp kiểm thử khác đăng nhập, nên khóa nó lại giữa
/// chừng sẽ làm đỏ những phép thử không liên quan gì tới lát cắt này. Dù vậy mỗi phép thử vẫn
/// tự trả trạng thái về <c>ACTIVE</c> trong khối <c>finally</c> — bộ kiểm thử dùng chung một
/// database cho cả lần chạy, nên một phép thử hỏng giữa chừng mà không dọn là một phép thử làm
/// hỏng lớp chạy sau nó.
/// </para>
/// <para>
/// Phép thử đáng giá nhất là <see cref="Tai_khoan_bi_khoa_mat_quyen_ngay_o_request_ke_tiep"/>.
/// Nó chứng minh khóa tài khoản thật sự có hiệu lực chứ không chỉ đổi một cột — và nó đi qua
/// đúng cơ chế mà BR-AUTH-022 hứa, cùng cơ chế mà lát cắt thu hồi phiên dựa vào.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class AccountStatusTests(SalonSysFactory factory)
{
    /// <summary>Chủ tiệm Bloom — một trong bốn tài khoản phụ mà không lớp kiểm thử nào khác dùng.</summary>
    private const string BloomOwnerId = "USR-TENANT-BLOOM";

    private const string BloomOwnerEmail = "ha.vu@bloomsalon.vn";
    private const string BloomOwnerPassword = "Tenant@2026";

    [Fact]
    public async Task Superadmin_khoa_va_mo_khoa_duoc_tai_khoan_chu_tiem()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        try
        {
            var locked = await Suspend(superadmin);

            Assert.Equal(HttpStatusCode.OK, locked.Status);
            Assert.Equal("SUSPENDED", Text(locked.Body.GetProperty("account"), "status"));

            var opened = await Restore(superadmin);

            Assert.Equal(HttpStatusCode.OK, opened.Status);
            Assert.Equal("ACTIVE", Text(opened.Body.GetProperty("account"), "status"));
        }
        finally
        {
            await Restore(superadmin);
        }
    }

    [Fact]
    public async Task Tai_khoan_bi_khoa_mat_quyen_ngay_o_request_ke_tiep()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);
        using var victim = SalonSysClient.Anonymous(factory);

        await victim.LoginAsync(BloomOwnerEmail, BloomOwnerPassword);

        try
        {
            // Trước khi bị khóa, phiên đọc lại được bình thường.
            Assert.Equal(HttpStatusCode.OK, (await victim.GetAsync("/api/auth/session")).Status);

            await Suspend(superadmin);

            // Đây là điều thật sự cần chứng minh: BR-AUTH-022 nói phép kiểm tra trạng thái tài
            // khoản chạy lại ở MỖI lần đọc phiên, nên người bị khóa mất quyền ngay ở request kế
            // tiếp chứ không đợi phiên hết hạn. Đây là request kế tiếp.
            var after = await victim.GetAsync("/api/auth/session");

            // 403 chứ KHÔNG phải 401, và khẳng định luôn mã lỗi vì đó mới là phần có chủ đích:
            // `GetCurrentAccountUseCase` ghi rõ frontend đưa người dùng về màn đăng nhập khi gặp
            // UNAUTHENTICATED. Trả 401 ở đây thì người vừa bị khóa bị đá về màn đăng nhập, đăng
            // nhập lại, nhận đúng lỗi ấy, và lặp mãi mà không bao giờ đọc được lý do thật.
            Assert.Equal(HttpStatusCode.Forbidden, after.Status);
            Assert.Equal("ACCOUNT_NOT_ACTIVE", after.ErrorCode);
        }
        finally
        {
            await Restore(superadmin);
        }
    }

    [Fact]
    public async Task Bi_khoa_thi_khong_dang_nhap_lai_duoc_nua()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        try
        {
            await Suspend(superadmin);

            using var blocked = SalonSysClient.Anonymous(factory);
            var refused = await blocked.LoginAsync(BloomOwnerEmail, BloomOwnerPassword);

            // BR-AUTH-021 — chỉ tài khoản Active mới đăng nhập được. Cùng mã lỗi với đường đọc
            // phiên, và `LoginUseCase` cố ý làm vậy để hai lối vào hệ thống không trả lời mâu
            // thuẫn nhau về cùng một tình huống.
            Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
            Assert.Equal("ACCOUNT_NOT_ACTIVE", refused.ErrorCode);

            await Restore(superadmin);

            using var allowed = SalonSysClient.Anonymous(factory);

            // Và mở khóa phải thật sự mở: khóa mà không có đường về là vô hiệu vĩnh viễn đội lốt
            // khóa tạm, đúng thứ mà việc tách Suspend khỏi Deactivate sinh ra để tránh.
            Assert.Equal(
                HttpStatusCode.OK,
                (await allowed.LoginAsync(BloomOwnerEmail, BloomOwnerPassword)).Status);
        }
        finally
        {
            await Restore(superadmin);
        }
    }

    [Fact]
    public async Task Chu_tiem_khong_khoa_duoc_tai_khoan_nao()
    {
        using var owner = await SalonSysClient.TenantAdminAsync(factory, "TEN-LUMIERE");

        var response = await Suspend(owner);

        // Ô TenantAdminAccounts chỉ Superadmin có (BR-AUTH-010). Nếu chủ tiệm gọi được thì một
        // người quản tiệm khóa được tài khoản của người quản tiệm khác.
        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Equal("FORBIDDEN", response.ErrorCode);
    }

    [Fact]
    public async Task Vo_hieu_vinh_vien_khong_dat_duoc_qua_duong_nay()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        var response = await superadmin.PatchAsync(
            $"/api/accounts/{BloomOwnerId}/status", new { status = "INACTIVE" });

        // INACTIVE là vô hiệu vĩnh viễn và BR-DEL-001 không cho xóa để tạo lại, nên nó phải có
        // nút riêng chứ không nấp sau cùng một tham số với thao tác khóa tạm.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);

        // Contract lỗi mang mảng error.fields để frontend gắn câu chữ vào đúng ô nhập, chứ
        // không chỉ ném một thông báo chung lên đầu biểu mẫu.
        var fields = response.Body.GetProperty("error").GetProperty("fields")
            .EnumerateArray()
            .Select(item => item.GetProperty("field").GetString())
            .ToArray();

        Assert.Contains("status", fields);
    }

    [Fact]
    public async Task Khoa_hai_lan_khong_phai_loi()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        try
        {
            var first = await Suspend(superadmin);
            var second = await Suspend(superadmin);

            Assert.Equal(HttpStatusCode.OK, first.Status);
            Assert.Equal(HttpStatusCode.OK, second.Status);
            Assert.Equal("SUSPENDED", Text(second.Body.GetProperty("account"), "status"));
        }
        finally
        {
            await Restore(superadmin);
        }
    }

    [Fact]
    public async Task Nhat_ky_ghi_su_kien_rieng_chu_khong_dung_lai_ACCOUNT_LOCKED()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        try
        {
            await Suspend(superadmin);

            var logs = await superadmin.GetAsync("/api/audit-logs?take=50");
            var events = logs.ValuesOf("entries", "event");

            // ACCOUNT_LOCKED là hệ quả của việc gõ sai mật khẩu năm lần — không ai bấm. Dòng của
            // thao tác khóa tay phải mang tên khác, nếu không thì sổ bảo mật trộn một sự cố kỹ
            // thuật với một quyết định có người chịu trách nhiệm.
            Assert.Contains("ACCOUNT_SUSPENDED", events);
        }
        finally
        {
            await Restore(superadmin);
        }
    }

    private Task<ApiResponse> Suspend(SalonSysClient client)
        => client.PatchAsync($"/api/accounts/{BloomOwnerId}/status", new { status = "SUSPENDED" });

    private Task<ApiResponse> Restore(SalonSysClient client)
        => client.PatchAsync($"/api/accounts/{BloomOwnerId}/status", new { status = "ACTIVE" });

    private static string? Text(System.Text.Json.JsonElement element, string property)
        => element.TryGetProperty(property, out var value) ? value.GetString() : null;
}
