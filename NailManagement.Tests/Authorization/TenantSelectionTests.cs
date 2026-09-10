using System.Net;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Authorization;

/// <summary>
/// Chủ tiệm đã đăng nhập nhưng chưa chọn tiệm — BR-AUTH-024, BR-AUTH-025.
///
/// <para>
/// Trạng thái này <b>không phải lỗi</b>: một tài khoản quản nhiều tiệm (BR-AUTH-023) luôn đi qua
/// màn chọn tiệm trước khi làm được việc gì. Nhưng máy chủ vẫn phải từ chối mọi thao tác cần
/// phạm vi tiệm cho tới lúc đó, và phải từ chối bằng một <b>mã lỗi riêng</b>.
/// </para>
/// <para>
/// Vì sao mã riêng đáng một bộ kiểm thử: cho tới ngày 24, ngoại lệ này ném <c>FORBIDDEN</c> —
/// đúng mã mà frontend dùng để nói "bạn không có quyền". Người dùng chỉ cần bấm chọn tiệm là đi
/// tiếp được lại đọc thấy một câu về quyền hạn. Bản thân chú thích trong mã nguồn khi ấy đã nói
/// là "có mã riêng", nên đây cũng là một phép kiểm chống lệch giữa lời và việc.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class TenantSelectionTests(SalonSysFactory factory)
{
    private const string Lumiere = "TEN-LUMIERE";

    [Fact]
    public async Task Chua_chon_tiem_thi_nhan_ma_TENANT_NOT_SELECTED_chu_khong_phai_FORBIDDEN()
    {
        using var client = SalonSysClient.Anonymous(factory);

        // Đăng nhập xong và DỪNG ở đó — không gọi chọn tiệm, đúng như người dùng vừa nhìn thấy
        // màn chọn tiệm mà chưa bấm gì.
        await client.LoginAsync(SalonSysClient.TenantAdminEmail, SalonSysClient.TenantAdminPassword);

        var refused = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);

        // Mã HTTP giữ nguyên 403; thứ phân biệt nằm ở mã lỗi trong thân phản hồi.
        Assert.Equal("TENANT_NOT_SELECTED", refused.ErrorCode);
    }

    [Fact]
    public async Task Chon_tiem_xong_thi_di_tiep_duoc()
    {
        using var client = SalonSysClient.Anonymous(factory);

        await client.LoginAsync(SalonSysClient.TenantAdminEmail, SalonSysClient.TenantAdminPassword);
        await client.SelectTenantAsync(Lumiere);

        var allowed = await client.GetAsync("/api/customers");

        // Vế còn lại của phép thử trên: mã mới không được biến thành một lời từ chối vĩnh viễn.
        Assert.Equal(HttpStatusCode.OK, allowed.Status);
        Assert.True(allowed.CountOf("customers") > 0);
    }
}
