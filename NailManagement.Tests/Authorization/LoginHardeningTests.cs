using System.Diagnostics;
using System.Net;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Authorization;

/// <summary>
/// Ba hàng rào của lệnh đăng nhập, kiểm bằng chính hành vi chứ không bằng lời khai của mã nguồn.
/// <para>
/// Cả ba đều thuộc loại "chạy đúng thì không ai thấy gì": không có màn hình nào hiện ra, không
/// có con số nào đổi. Nghĩa là nếu chúng hỏng thì cũng không ai thấy gì — nên chúng phải có
/// phép thử, hoặc chúng sẽ lặng lẽ mục đi.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class LoginHardeningTests(SalonSysFactory factory)
{
    private const string UnknownEmail = "khong-ai-dung-email-nay@salonsys.vn";
    private const string WrongPassword = "Sai@2026Khong-Dung-Dau";

    /// <summary>
    /// Tài khoản dùng cho phép đo thời gian — chủ tiệm Bloom, KHÔNG phải Superadmin.
    /// <para>
    /// Mỗi lần đo là một lần đăng nhập sai, và năm lần sai liên tiếp thì <c>AuthPolicy</c> khóa
    /// tài khoản 15 phút. Đo trên tài khoản Superadmin là khóa mất tài khoản mà gần như mọi lớp
    /// kiểm thử khác dùng để đăng nhập — cả bộ sẽ đỏ vì một lý do không liên quan.
    /// </para>
    /// <para>
    /// Ngay cả trên tài khoản này cũng phải đếm: <b>ba lượt đo cộng một lượt hâm nóng là bốn</b>,
    /// vẫn dưới ngưỡng năm. Và phép thử kết thúc bằng một lần đăng nhập ĐÚNG để
    /// <c>RegisterSuccessfulLogin</c> xóa sạch bộ đếm, không để lại dấu vết cho lớp chạy sau.
    /// </para>
    /// </summary>
    private const string ProbeEmail = "ha.vu@bloomsalon.vn";

    private const string ProbePassword = "Tenant@2026";

    // ── 1. Không lộ email nào có thật ────────────────────────────────────────

    /// <summary>
    /// Câu trả lời phải giống nhau: cùng mã HTTP, cùng mã lỗi, cùng câu chữ.
    /// </summary>
    [Fact]
    public async Task Email_la_va_email_that_tra_ve_cung_mot_cau_tra_loi()
    {
        using var client = SalonSysClient.Anonymous(factory);

        var unknown = await client.LoginAsync(UnknownEmail, WrongPassword);
        var real = await client.LoginAsync(ProbeEmail, WrongPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, real.Status);
        Assert.Equal(real.ErrorCode, unknown.ErrorCode);
        Assert.Equal(Message(real), Message(unknown));

        // Dọn bộ đếm sai của tài khoản dò, xem chú thích ở ProbeEmail.
        await client.LoginAsync(ProbeEmail, ProbePassword);
    }

    /// <summary>
    /// Và phải giống nhau cả về <b>thời gian</b> — đây mới là phép thử thật sự của lần vá.
    /// <para>
    /// Trước khi vá, nhánh "không tìm thấy tài khoản" thoát ra trước khi chạy PBKDF2, nên nó
    /// trả lời nhanh hơn hàng chục lần. Chênh lệch ấy đo được bằng đồng hồ, và nó dựng lại đúng
    /// phép dò email mà thông điệp giống nhau tưởng đã chặn.
    /// </para>
    /// <para>
    /// <b>Vì sao phép thử này không mong manh.</b> PBKDF2 chạy 210.000 vòng, tốn hàng chục
    /// mili-giây; nhánh thoát sớm tốn chưa tới một. Khoảng cách thật là vài chục lần, nên ngưỡng
    /// đặt ở <b>4 lần</b> vẫn còn rất rộng so với nhiễu của một máy đang bận. Lấy trung vị của ba
    /// lượt để một lần kẹt ngẫu nhiên không kéo lệch kết quả, và có một lượt hâm nóng bỏ đi vì
    /// lần gọi đầu tiên còn tốn thêm thời gian nạp mã.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Email_la_khong_tra_loi_nhanh_hon_email_that()
    {
        using var client = SalonSysClient.Anonymous(factory);

        // Hâm nóng: bỏ kết quả đi, chỉ để mọi thứ được nạp và biên dịch JIT xong. Lượt này là
        // lần sai thứ nhất trên bốn lần cho phép.
        await client.LoginAsync(ProbeEmail, WrongPassword);
        await client.LoginAsync(UnknownEmail, WrongPassword);

        var real = Median(await MeasureAsync(client, ProbeEmail));
        var unknown = Median(await MeasureAsync(client, UnknownEmail));

        // Đăng nhập đúng để xóa bộ đếm sai TRƯỚC khi khẳng định. Đặt sau `Assert` thì một lần
        // đỏ sẽ để lại tài khoản mang ba lần sai cho lớp chạy kế tiếp.
        await client.LoginAsync(ProbeEmail, ProbePassword);

        // Chặn đúng chiều đáng lo: nhánh email lạ trả lời NHANH hơn hẳn. Chiều ngược lại vô
        // hại — không ai suy ra được gì từ việc email thật trả lời nhanh hơn.
        Assert.True(
            unknown * 4 >= real,
            $"Email lạ trả lời quá nhanh so với email thật — lộ email nào có thật qua thời gian. "
            + $"Trung vị: lạ {unknown}ms, thật {real}ms.");
    }

    // ── 2. Cookie phiên ──────────────────────────────────────────────────────

    /// <summary>
    /// Cookie phiên phải mang <c>HttpOnly</c> và <c>SameSite=Strict</c> ở mọi môi trường.
    /// <para>
    /// <c>Secure</c> thì <b>không</b> kiểm ở đây, và đó là chủ ý: bộ kiểm thử chạy ở môi trường
    /// Development nên cờ ấy đúng ra phải tắt — bật lên thì chính bộ kiểm thử không đăng nhập
    /// nổi qua HTTP. Phép thử canh nó nằm ngay dưới.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Cookie_phien_mang_HttpOnly_va_SameSite_Strict()
    {
        using var client = SalonSysClient.Anonymous(factory);

        using var response = await client.LoginRawAsync(SalonSysClient.SuperAdminEmail, SalonSysClient.SuperAdminPassword);
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("salonsys_session=", StringComparison.Ordinal));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ở môi trường Development, cờ <c>Secure</c> phải TẮT.
    /// <para>
    /// Nghe như kiểm ngược, nhưng đây chính là phép thử giữ cho buổi demo không hỏng: cả hai
    /// profile chạy đều là Development và đều dùng HTTP, nên nếu ai đó đổi điều kiện thành
    /// "luôn bật" thì không ai đăng nhập được nữa — và phép thử này đỏ trước khi điều đó xảy ra
    /// trên máy người chấm.
    /// </para>
    /// </summary>
    [Fact]
    public async Task O_moi_truong_Development_thi_cookie_khong_dat_Secure()
    {
        using var client = SalonSysClient.Anonymous(factory);

        using var response = await client.LoginRawAsync(SalonSysClient.SuperAdminEmail, SalonSysClient.SuperAdminPassword);
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("salonsys_session=", StringComparison.Ordinal));

        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Và ở môi trường KHÁC Development thì cờ <c>Secure</c> phải BẬT.
    /// <para>
    /// Đây mới là phép thử của lần vá. Hai phép thử phía trên chạy ở Development, nơi cờ đúng ra
    /// phải tắt — và nó tắt cả trước lẫn sau lần vá, nên chúng không phân biệt được bản đã sửa
    /// với bản ghi cứng <c>Secure = false</c>. Chỉ khi hỏi ở phía bên kia của điều kiện thì câu
    /// trả lời mới khác nhau.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Ngoai_moi_truong_Development_thi_cookie_bat_Secure()
    {
        using var production = new ProductionLikeFactory();
        using var client = SalonSysClient.Anonymous(production);

        using var response = await client.LoginRawAsync(
            SalonSysClient.SuperAdminEmail, SalonSysClient.SuperAdminPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("salonsys_session=", StringComparison.Ordinal));

        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    // ── 3. Giới hạn theo IP ──────────────────────────────────────────────────

    /// <summary>
    /// Quá số lần cho phép thì bị chặn, kể cả khi mật khẩu đúng.
    /// <para>
    /// Chạy trên <see cref="ThrottledLoginFactory"/> — một máy chủ riêng có trần thấp. Máy chủ
    /// dùng chung nới trần lên rất cao để cả lần chạy không tự khóa mình, nên ở đó không chạm
    /// tới hàng rào này được.
    /// </para>
    /// <para>
    /// Lần gọi cuối dùng <b>mật khẩu đúng</b>, có chủ ý: nó chứng minh hàng rào chặn ở tầng
    /// vận chuyển, trước khi bất kỳ phép kiểm mật khẩu nào chạy. Chặn được cả một lần đăng nhập
    /// hợp lệ mới là chặn thật.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Vuot_tran_dang_nhap_theo_IP_thi_bi_chan()
    {
        using var throttled = new ThrottledLoginFactory();
        using var client = SalonSysClient.Anonymous(throttled);

        for (var attempt = 0; attempt < ThrottledLoginFactory.PermitLimit; attempt++)
        {
            var allowed = await client.LoginAsync(UnknownEmail, WrongPassword);

            Assert.Equal(HttpStatusCode.Unauthorized, allowed.Status);
        }

        var blocked = await client.LoginAsync(SalonSysClient.SuperAdminEmail, SalonSysClient.SuperAdminPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.Status);
        Assert.Equal("TOO_MANY_REQUESTS", blocked.ErrorCode);
    }

    /// <summary>
    /// Giới hạn chỉ áp cho lệnh đăng nhập, không áp cho phần còn lại của API.
    /// <para>
    /// Gắn nhầm cho cả controller thì lệnh đọc phiên cũng bị đếm — mà giao diện gọi nó ở mỗi
    /// lần tải trang, nên một người dùng bình thường sẽ tự khóa mình chỉ bằng cách bấm chuyển
    /// màn hình vài chục lần.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Gioi_han_khong_cham_toi_cac_endpoint_khac()
    {
        using var throttled = new ThrottledLoginFactory();
        using var client = await SalonSysClient.SuperAdminAsync(throttled);

        // Gọi gấp nhiều lần trần đăng nhập. Nếu chính sách bị gắn cho cả controller thì những
        // lời gọi cuối sẽ nhận 429.
        for (var attempt = 0; attempt < ThrottledLoginFactory.PermitLimit * 3; attempt++)
        {
            var response = await client.GetAsync("/api/auth/session");

            Assert.Equal(HttpStatusCode.OK, response.Status);
        }
    }

    // ── Phụ trợ ──────────────────────────────────────────────────────────────

    private static async Task<List<long>> MeasureAsync(SalonSysClient client, string identifier)
    {
        var samples = new List<long>();

        // Ba lượt, không hơn: cộng với lượt hâm nóng là bốn lần sai, vẫn dưới ngưỡng khóa năm
        // lần của AuthPolicy. Trung vị của ba lượt đủ để một lần kẹt ngẫu nhiên không kéo lệch.
        for (var round = 0; round < 3; round++)
        {
            var watch = Stopwatch.StartNew();
            await client.LoginAsync(identifier, WrongPassword);
            watch.Stop();

            samples.Add(watch.ElapsedMilliseconds);
        }

        return samples;
    }

    private static long Median(List<long> samples)
    {
        samples.Sort();

        return samples[samples.Count / 2];
    }

    private static string? Message(ApiResponse response)
        => response.Body.TryGetProperty("error", out var error)
            && error.TryGetProperty("message", out var message)
            ? message.GetString()
            : null;
}
