using Microsoft.Extensions.DependencyInjection;
using NailManagement.Domain.Auth;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Concurrency;

/// <summary>
/// Ghi đè mất dấu (<i>lost update</i>) trên hai bảng của lát cắt xác thực.
///
/// <para>
/// Đây là bộ kiểm thử duy nhất của dự án <b>không</b> đi qua HTTP, và lý do nằm ở chính thứ nó
/// đi kiểm: cần hai request đọc cùng một bản ghi rồi ghi <b>đè lên nhau theo một thứ tự định
/// trước</b>. Qua HTTP thì thứ tự ấy do bộ lập lịch của hệ điều hành quyết, nên phép thử sẽ khi
/// xanh khi đỏ và không chứng minh được gì. Ở đây mỗi "request" là một scope dịch vụ riêng —
/// đúng thứ mà máy chủ cấp cho mỗi request thật — nên thứ tự là do bài kiểm thử đặt.
/// </para>
/// <para>
/// Lỗi mà cả ba phép thử đi giữ là cùng một lỗi: kho dữ liệu gọi <c>DbSet.Update(...)</c> trên
/// một bản ghi <b>đã nằm trong bộ theo dõi thay đổi</b>. Hàm đó đánh dấu mọi cột là đã sửa, nên
/// câu <c>UPDATE</c> sinh ra mang theo cả những cột mà request này chưa từng chạm — với giá trị
/// của bản chụp lúc nó đọc lên. Hai request chồng nhau, request chậm hơn thắng, và thứ bị nuốt
/// mất là một quyết định về <b>quyền truy cập</b>.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class LostUpdateTests(SalonSysFactory factory)
{
    private const string SuperAdminId = "USR-SUPERADMIN";
    private const string ReceptionistId = "USR-RECEPTION-NAILE";
    private const string Muse = "TEN-MUSE";

    [Fact]
    public async Task Cham_phien_khong_lam_song_lai_mot_phien_vua_bi_thu_hoi()
    {
        var sessionId = await IssueSessionAsync(SuperAdminId);
        var now = DateTimeOffset.UtcNow;

        // Request A đọc phiên lên. Từ giây phút này, bản chụp của A có RevokedAt = null.
        using var scopeA = factory.Services.CreateScope();
        var sessionsA = scopeA.ServiceProvider.GetRequiredService<ISessionRepository>();
        var seenByA = await sessionsA.FindByIdAsync(sessionId);

        // Trong lúc A còn đang làm việc, request B thu hồi đúng phiên ấy — chủ tiệm bấm "đăng
        // xuất thiết bị này" ở màn quản trị phiên (BR-AUTH-033).
        using (var scopeB = factory.Services.CreateScope())
        {
            var sessionsB = scopeB.ServiceProvider.GetRequiredService<ISessionRepository>();
            var seenByB = await sessionsB.FindByIdAsync(sessionId);

            seenByB!.Revoke(now);
            await sessionsB.UpdateAsync(seenByB);
        }

        // A xong việc và lưu lại dấu hoạt động cuối. Nó chỉ định ghi LastActive.
        seenByA!.Touch(now.AddSeconds(1));
        await sessionsA.UpdateAsync(seenByA);

        var after = await ReadSessionAsync(sessionId);

        // Phép thu hồi phải còn nguyên. Nếu dòng này đỏ nghĩa là một phiên đã bị thu hồi vẫn
        // đăng nhập được — hàng rào BR-AUTH-033 bị chính lượt chạm phiên của người dùng gỡ ra.
        Assert.NotNull(after!.RevokedAt);
        Assert.False(after.IsValidAt(now.AddSeconds(2)));
    }

    [Fact]
    public async Task Cham_phien_khong_keo_tiem_dang_lam_viec_ve_gia_tri_cu()
    {
        var sessionId = await IssueSessionAsync(SuperAdminId);
        var now = DateTimeOffset.UtcNow;

        // A đọc phiên khi nó chưa gắn tiệm nào.
        using var scopeA = factory.Services.CreateScope();
        var sessionsA = scopeA.ServiceProvider.GetRequiredService<ISessionRepository>();
        var seenByA = await sessionsA.FindByIdAsync(sessionId);

        Assert.Null(seenByA!.ActiveTenantId);

        // B đổi tiệm đang làm việc (BR-AUTH-025).
        using (var scopeB = factory.Services.CreateScope())
        {
            var sessionsB = scopeB.ServiceProvider.GetRequiredService<ISessionRepository>();
            var seenByB = await sessionsB.FindByIdAsync(sessionId);

            seenByB!.SetActiveTenant(Muse, now);
            await sessionsB.UpdateAsync(seenByB);
        }

        seenByA.Touch(now.AddSeconds(1));
        await sessionsA.UpdateAsync(seenByA);

        var after = await ReadSessionAsync(sessionId);

        // Kéo phiên về tiệm cũ không chỉ là phiền: mọi truy vấn sau đó đi qua bộ lọc theo tiệm
        // ở NailDbContext, nên người dùng sẽ đọc và ghi nhầm sang tiệm mình vừa rời khỏi.
        Assert.Equal(Muse, after!.ActiveTenantId);
    }

    [Fact]
    public async Task Dang_nhap_thanh_cong_khong_mo_lai_tai_khoan_vua_bi_khoa()
    {
        var now = DateTimeOffset.UtcNow;

        try
        {
            // A đang ở giữa một lượt đăng nhập thành công: mật khẩu đúng, và nó ghi lại mốc
            // đăng nhập cùng việc xóa bộ đếm sai mật khẩu.
            using var scopeA = factory.Services.CreateScope();
            var usersA = scopeA.ServiceProvider.GetRequiredService<IUserRepository>();
            var seenByA = await usersA.FindByIdAsync(ReceptionistId);

            Assert.Equal(AccountStatus.Active, seenByA!.Status);

            seenByA.RegisterSuccessfulLogin(now);

            // Ngay lúc ấy, quản trị viên khóa tài khoản này ở một request khác.
            await TestDatabase.SetAccountStatusAsync(factory, ReceptionistId, AccountStatus.Suspended);

            await usersA.UpdateAsync(seenByA);

            var after = await ReadUserAsync(ReceptionistId);

            // BR-AUTH-021: tài khoản bị khóa thì không được vào nữa. Lượt đăng nhập đang dở
            // không được phép mở lại cửa cho chính tài khoản vừa bị đóng.
            Assert.Equal(AccountStatus.Suspended, after!.Status);
        }
        finally
        {
            // Trả lại trạng thái cho các lớp kiểm thử sau — chúng dùng chung một database.
            await TestDatabase.SetAccountStatusAsync(factory, ReceptionistId, AccountStatus.Active);
        }
    }

    /// <summary>Một phiên mới tinh để phép thử tự do thu hồi mà không đụng phiên của lớp khác.</summary>
    private async Task<string> IssueSessionAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();

        var id = $"SES-CONCURRENCY-{Guid.NewGuid():N}";

        await sessions.AddAsync(
            AppSession.Issue(id, userId, DateTimeOffset.UtcNow, TimeSpan.FromHours(8), "127.0.0.1", "xunit"));

        return id;
    }

    /// <summary>Đọc lại từ một scope thứ ba, để không đọc trúng bản chụp còn trong bộ nhớ của A hay B.</summary>
    private async Task<AppSession?> ReadSessionAsync(string sessionId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ISessionRepository>()
            .FindByIdAsync(sessionId);
    }

    /// <inheritdoc cref="ReadSessionAsync"/>
    private async Task<AppUser?> ReadUserAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .FindByIdAsync(userId);
    }
}
