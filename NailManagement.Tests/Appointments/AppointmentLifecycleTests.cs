using System.Net;
using NailManagement.Tests.Infrastructure;

using static NailManagement.Tests.Scenarios.SalonScenario;

namespace NailManagement.Tests.Appointments;

/// <summary>
/// Sơ đồ chuyển trạng thái lịch hẹn ở mục 16.1 — BR-APT-020/021/022/040/041.
/// <para>
/// Điều đáng kiểm ở đây không phải "đường đúng có chạy không" — mạch demo đã đi qua nó mỗi
/// ngày. Đáng kiểm là <b>những đường không có trong sơ đồ có bị chặn không</b>, vì đó là thứ
/// duy nhất giữ cho bảng trạng thái mang nghĩa. Một hệ thống nhận mọi bước nhảy vẫn chạy trơn
/// tru trong buổi demo, rồi báo cáo doanh thu đếm phải những buổi làm chưa từng diễn ra.
/// </para>
/// <para>
/// Phép thử đi qua HTTP nên nó khẳng định luôn một điều mà gọi thẳng entity không thấy được:
/// cùng một đường dẫn <c>PATCH /{id}/status</c> phục vụ cả bảy trạng thái, nên phép kiểm phải
/// nằm trên <b>cặp</b> (đang ở đâu, đi tới đâu) chứ không nằm trên đường dẫn.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class AppointmentLifecycleTests(SalonSysFactory factory)
{
    /// <summary>
    /// BR-APT-022 — mọi bước nhảy không có trong sơ đồ đều bị từ chối.
    /// <para>
    /// Bốn cặp được chọn vì mỗi cặp là một cách <b>bỏ qua một bước có thật ở tiệm</b>:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>PENDING → CHECKED_IN</c> — khách chưa được xác nhận đã bị ghi là đã đến.</item>
    ///   <item><c>PENDING → NO_SHOW</c> — đánh dấu khách không đến một buổi hẹn chưa ai chốt.</item>
    ///   <item><c>CONFIRMED → IN_SERVICE</c> — bắt đầu làm cho một người chưa bước vào tiệm.</item>
    ///   <item><c>CONFIRMED → COMPLETED</c> — đóng luôn một buổi chưa hề bắt đầu.</item>
    /// </list>
    /// <para>
    /// Cặp cuối đi một đường khác hẳn ba cặp trên: đích <c>COMPLETED</c> rẽ sang nhánh đóng tay
    /// của BR-APT-027, nên nó qua được phép kiểm quyền của chủ tiệm rồi mới bị luật vòng đời
    /// chặn. Giữ nó trong cùng bảng là để chắc rằng ngoại lệ ấy <b>không</b> tiện tay mở luôn
    /// một cửa sau vào trạng thái cuối.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("PENDING", "CHECKED_IN")]
    [InlineData("PENDING", "NO_SHOW")]
    [InlineData("CONFIRMED", "IN_SERVICE")]
    [InlineData("CONFIRMED", "COMPLETED")]
    public async Task Buoc_nhay_khong_co_trong_so_do_bi_tu_choi(string from, string to)
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var appointmentId = await BookAtAsync(admin, from);

        var refused = await admin.PatchAsync(
            $"/api/appointments/{appointmentId}/status", new { status = to });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Equal("VALIDATION_FAILED", refused.ErrorCode);
        Assert.Contains("status", FieldNames(refused));

        // Bị từ chối nghĩa là KHÔNG có gì đổi. Thiếu vế này thì một lỗi ghi trước rồi mới kiểm
        // vẫn qua được: người dùng nhận một thông báo lỗi trong khi bản ghi đã đi mất.
        Assert.Equal(from, await AppointmentStatusAsync(admin, appointmentId));
    }

    /// <summary>
    /// BR-APT-041 — ba trạng thái cuối không quay lại được.
    /// <para>
    /// Đây là luật giữ cho lịch sử của tiệm còn đọc được. Cho phép mở lại một lịch đã hủy thì
    /// không còn cách nào phân biệt một buổi thật sự diễn ra với một buổi được sửa cho thành
    /// đã diễn ra, và toàn bộ báo cáo dựng trên đó mất chỗ dựa.
    /// </para>
    /// <para>
    /// <c>COMPLETED</c> cố ý vắng mặt ở bảng này: nó đã có phép thử riêng ở lát cắt thu tiền,
    /// và đường tới nó phải đi qua tiền thật nên dựng lại ở đây là chép việc.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("CANCELLED", "CONFIRMED")]
    [InlineData("CANCELLED", "CHECKED_IN")]
    [InlineData("NO_SHOW", "CHECKED_IN")]
    public async Task Trang_thai_cuoi_khong_quay_lai_duoc(string final, string attempt)
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var appointmentId = await BookAtAsync(admin, "CONFIRMED");

        await AdvanceAsync(admin, appointmentId, final);

        var refused = await admin.PatchAsync(
            $"/api/appointments/{appointmentId}/status", new { status = attempt });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Equal(final, await AppointmentStatusAsync(admin, appointmentId));
    }

    /// <summary>
    /// Một chuỗi ký tự không phải tên trạng thái nào phải nhận đúng câu giải thích, không phải
    /// một lỗi chung chung — và tuyệt đối không phải 500.
    /// <para>
    /// Đường này có thật: giao diện gửi <c>nextStatuses</c> ngược lên máy chủ, nên một lần đổi
    /// tên trạng thái ở một bên mà bên kia chưa theo kịp sẽ rơi thẳng vào đây.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Trang_thai_khong_co_trong_bang_bi_tu_choi_kem_danh_sach_gia_tri_hop_le()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var appointmentId = await BookAtAsync(admin, "CONFIRMED");

        var refused = await admin.PatchAsync(
            $"/api/appointments/{appointmentId}/status", new { status = "DONE" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Equal("VALIDATION_FAILED", refused.ErrorCode);
        Assert.Contains("status", FieldNames(refused));
        Assert.Contains("CHECKED_IN", refused.Body.GetProperty("error").GetProperty("message").GetString());
    }

    /// <summary>
    /// <c>nextStatuses</c> trên DTO phải là <b>chính</b> sơ đồ, vì giao diện dựng các nút bấm
    /// từ nó.
    /// <para>
    /// Lệch một ô là màn hình mọc ra một cái nút bấm vào sẽ bị từ chối, hoặc thiếu mất một nút
    /// mà người ở quầy cần — cả hai đều là lỗi người dùng gặp trước khi lập trình viên gặp.
    /// Phép thử đi hết một lượt đời của lịch hẹn thay vì kiểm rời từng trạng thái, vì thứ đáng
    /// khẳng định là danh sách ấy <b>đổi theo</b> từng bước chứ không phải một hằng số.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Danh_sach_buoc_di_tiep_khop_voi_so_do_o_tung_chang()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var appointmentId = await BookAtAsync(admin, "PENDING");

        Assert.Equal(["CONFIRMED", "CANCELLED"], await NextStatusesAsync(admin, appointmentId));

        await AdvanceAsync(admin, appointmentId, "CONFIRMED");
        Assert.Equal(["CHECKED_IN", "CANCELLED", "NO_SHOW"], await NextStatusesAsync(admin, appointmentId));

        await AdvanceAsync(admin, appointmentId, "CHECKED_IN");
        Assert.Equal(["IN_SERVICE", "CANCELLED", "NO_SHOW"], await NextStatusesAsync(admin, appointmentId));

        // BR-APT-040 — đang phục vụ dở thì không hủy được nữa, chỉ còn đường kết thúc.
        await AdvanceAsync(admin, appointmentId, "IN_SERVICE");
        Assert.Equal(["COMPLETED"], await NextStatusesAsync(admin, appointmentId));

        // BR-APT-041 — hết đường.
        await AdvanceAsync(admin, appointmentId, "COMPLETED");
        Assert.Empty(await NextStatusesAsync(admin, appointmentId));
    }

    /// <summary>
    /// BR-APT-023/025 — dời giờ chỉ được phép khi lịch còn ở <c>PENDING</c> hoặc
    /// <c>CONFIRMED</c>, hẹp hơn hẳn phạm vi của lệnh sửa trọn.
    /// <para>
    /// Khách đã ngồi vào ghế thì giờ hẹn không còn là một dự định nữa; sửa nó là sửa lại lịch
    /// sử của một buổi làm đang diễn ra. Phép thử dừng ở <c>CHECKED_IN</c> vì đó là bước đầu
    /// tiên vượt ra ngoài phạm vi ấy — chặn được ở đây thì các trạng thái sau không có đường
    /// nào khác để lọt.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Khach_da_den_thi_khong_doi_gio_hen_duoc_nua()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var appointmentId = await BookAtAsync(admin, "CONFIRMED");
        var before = await admin.GetAsync($"/api/appointments/{appointmentId}");
        var startAt = before.Body.GetProperty("appointment").GetProperty("startAt").GetDateTimeOffset();

        await AdvanceAsync(admin, appointmentId, "CHECKED_IN");

        var refused = await admin.PatchAsync(
            $"/api/appointments/{appointmentId}/schedule", new { startAt = startAt.AddDays(1) });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Equal("VALIDATION_FAILED", refused.ErrorCode);
    }

    // ── Dựng dữ liệu ─────────────────────────────────────────────────────────

    /// <summary>
    /// Một lịch hẹn mới ở đúng trạng thái khởi tạo cho trước — BR-APT-021 chỉ cho phép
    /// <c>PENDING</c> hoặc <c>CONFIRMED</c>, và cả hai đều dựng thẳng được không cần bước trung gian.
    /// </summary>
    private static async Task<string> BookAtAsync(SalonSysClient client, string status)
    {
        var created = await TryBookAsync(
            client,
            await ActiveCustomerIdAsync(client),
            await TechnicianIdAsync(client, BranchQ3),
            NextSlot(),
            [Text(await ActiveServiceAsync(client), "id")!],
            status);

        Assert.Equal(HttpStatusCode.Created, created.Status);

        return Text(created.Body.GetProperty("appointment"), "id")!;
    }

    private static async Task<IReadOnlyList<string?>> NextStatusesAsync(
        SalonSysClient client, string appointmentId)
    {
        var response = await client.GetAsync($"/api/appointments/{appointmentId}");

        Assert.Equal(HttpStatusCode.OK, response.Status);

        return [.. response.Body.GetProperty("appointment").GetProperty("nextStatuses")
            .EnumerateArray().Select(status => status.GetString())];
    }
}
