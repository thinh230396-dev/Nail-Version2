using System.Net;
using NailManagement.Domain.Auth;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Authorization;

/// <summary>
/// BR-AUTH-022 — trạng thái tài khoản được kiểm ở <b>mỗi request</b>, không phải chỉ lúc đăng nhập.
/// <para>
/// Đây là lý do quyết định 11 chọn cookie kèm bảng phiên thay vì JWT: một JWT đã phát ra thì
/// không thu hồi được giữa chừng, nên tài khoản bị khóa vẫn dùng được cho tới khi thẻ hết hạn.
/// Phép thử này chính là bằng chứng cho lựa chọn đó — nó chỉ xanh khi phép kiểm nằm trong
/// <c>SessionMiddleware</c> và chạy trước mọi bộ lọc quyền.
/// </para>
/// <para>
/// Đặt sai chỗ phép kiểm này là một lỗ hổng im lặng: mọi thứ vẫn chạy đúng trong lúc phát triển,
/// và chỉ lộ ra đúng lúc có người cần khóa một tài khoản gấp.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class SessionRevalidationTests(SalonSysFactory factory)
{
    private const string ReceptionistUserId = "USR-RECEPTION-NAILE";

    [Theory]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Inactive)]
    public async Task Tai_khoan_bi_khoa_giua_phien_bi_tu_choi_o_request_ke_tiep(AccountStatus status)
    {
        using var client = await SalonSysClient.ReceptionistAsync(factory);

        // Phiên đang chạy bình thường ngay trước khi tài khoản bị khóa.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/customers")).Status);

        await TestDatabase.SetAccountStatusAsync(factory, ReceptionistUserId, status);

        try
        {
            var next = await client.GetAsync("/api/customers");

            Assert.Equal(HttpStatusCode.Forbidden, next.Status);

            // Mã lỗi riêng để người dùng đọc được "tài khoản đã bị khóa" thay vì "phiên hết hạn"
            // rồi loay hoay đăng nhập lại mãi không hiểu vì sao.
            Assert.Equal("ACCOUNT_NOT_ACTIVE", next.ErrorCode);
        }
        finally
        {
            await TestDatabase.SetAccountStatusAsync(factory, ReceptionistUserId, AccountStatus.Active);
        }
    }

    /// <summary>
    /// BR-AUTH-021 — tài khoản không còn hoạt động thì cũng không đăng nhập lại được. Hai phép
    /// chặn ở hai chỗ khác nhau: một ở lúc đăng nhập, một ở mỗi request.
    /// </summary>
    [Fact]
    public async Task Tai_khoan_bi_khoa_khong_dang_nhap_lai_duoc()
    {
        await TestDatabase.SetAccountStatusAsync(factory, ReceptionistUserId, AccountStatus.Suspended);

        try
        {
            using var client = SalonSysClient.Anonymous(factory);

            var login = await client.LoginAsync(
                SalonSysClient.ReceptionistEmail, SalonSysClient.ReceptionistPassword);

            Assert.Equal(HttpStatusCode.Forbidden, login.Status);
            Assert.Equal("ACCOUNT_NOT_ACTIVE", login.ErrorCode);
        }
        finally
        {
            await TestDatabase.SetAccountStatusAsync(factory, ReceptionistUserId, AccountStatus.Active);
        }
    }

    /// <summary>Chưa đăng nhập thì mọi endpoint nghiệp vụ đều trả 401, không phải 403.</summary>
    [Fact]
    public async Task Chua_dang_nhap_thi_bi_tu_choi_voi_ma_rieng()
    {
        using var anonymous = SalonSysClient.Anonymous(factory);

        var response = await anonymous.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Equal("UNAUTHENTICATED", response.ErrorCode);
    }
}
