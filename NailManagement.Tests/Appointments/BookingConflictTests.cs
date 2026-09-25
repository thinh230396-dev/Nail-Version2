using System.Globalization;
using System.Net;
using NailManagement.Tests.Infrastructure;

using static NailManagement.Tests.Scenarios.SalonScenario;

namespace NailManagement.Tests.Appointments;

/// <summary>
/// Chống trùng lịch kỹ thuật viên — BR-APT-010/011/012.
/// <para>
/// §6 của lộ trình xếp luật này vào bốn hạng mục <b>tuyệt đối không cắt</b>, cùng nhóm với
/// công thức tiền. Lý do rất cụ thể: một lần đặt trùng lọt lưới không báo lỗi ở đâu cả — nó
/// chỉ hiện ra vào đúng lúc hai khách cùng ngồi xuống trước mặt một kỹ thuật viên.
/// </para>
/// <para>
/// Ba luật đi cùng nhau và phải kiểm cùng nhau, vì mỗi luật một mình đều dựng ra được một hệ
/// thống sai: chỉ có BR-APT-011 thì thời gian dọn dẹp bị bỏ quên và hai khách xếp sát đến mức
/// không kịp trở tay; chỉ có BR-APT-010 thì một lịch đã hủy vẫn giữ chỗ mãi mãi.
/// </para>
/// <para>
/// Mọi phép thử ở đây đọc <c>durationMinutes</c>, <c>bufferMinutes</c> và <c>endAt</c> từ
/// <b>chính phản hồi của máy chủ</b> thay vì chép cứng con số của bảng giá mẫu. Chép cứng là
/// dựng một phép tính thứ hai bên cạnh phép tính của máy chủ, và khi hai phép tính lệch nhau
/// thì phép thử đỏ mà không chỉ ra được bên nào sai.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class BookingConflictTests(SalonSysFactory factory)
{
    /// <summary>
    /// BR-APT-011 — chặn cứng, và câu chữ phải đủ để lễ tân xếp lại lịch ngay mà không phải đi
    /// tra ở màn khác.
    /// <para>
    /// Phép thử khẳng định cả <b>nội dung</b> thông báo chứ không chỉ mã lỗi, vì contract lỗi
    /// chỉ có ba trường và không mang được một đối tượng đính kèm: nếu câu này không nói ra ai
    /// đang giữ chỗ và giữ tới mấy giờ thì người ở quầy không có gì để trả lời khách đang đứng
    /// trước mặt. Mã lỗi riêng <c>SLOT_CONFLICT</c> — không phải <c>VALIDATION_FAILED</c> — là
    /// để giao diện mở được đúng bảng giờ của kỹ thuật viên ấy thay vì chỉ bôi đỏ một ô nhập.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Dat_trung_gio_cua_cung_ky_thuat_vien_bi_chan_kem_cau_chu_du_de_xep_lai()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var customerId = await ActiveCustomerIdAsync(admin);
        var staffId = await TechnicianIdAsync(admin, BranchQ3);
        var serviceId = Text(await ActiveServiceAsync(admin), "id")!;
        var slot = NextSlot();

        var first = await TryBookAsync(admin, customerId, staffId, slot, [serviceId]);

        Assert.Equal(HttpStatusCode.Created, first.Status);

        // Lấn đúng nửa tiếng vào lịch vừa đặt.
        var clash = await TryBookAsync(admin, customerId, staffId, slot.AddMinutes(30), [serviceId]);

        Assert.Equal(HttpStatusCode.Conflict, clash.Status);
        Assert.Equal("SLOT_CONFLICT", clash.ErrorCode);
        Assert.Contains("startAt", FieldNames(clash));

        var message = clash.Body.GetProperty("error").GetProperty("message").GetString()!;
        var staffName = Text(first.Body.GetProperty("appointment"), "staffName")!;

        Assert.Contains(staffName, message);
        Assert.Contains(slot.ToString("HH:mm", CultureInfo.InvariantCulture), message);
        Assert.Contains(slot.ToString("dd/MM", CultureInfo.InvariantCulture), message);
    }

    /// <summary>
    /// BR-APT-011 dùng phép so <b>nghiêm ngặt</b>, nên hai lịch nối đuôi nhau — cái này bắt đầu
    /// đúng lúc cái kia kết thúc — không phải là trùng.
    /// <para>
    /// Đây là ranh giới thật của luật, và là chỗ dễ sai nhất: đổi <c>&lt;</c> thành <c>&lt;=</c>
    /// vẫn qua được mọi phép thử chỉ ném hai lịch chồng hẳn lên nhau, nhưng nó khóa mất khung
    /// giờ liền kề và kỹ thuật viên mất một suất khách mỗi lần.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Hai_lich_noi_duoi_nhau_khong_bi_coi_la_trung()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var customerId = await ActiveCustomerIdAsync(admin);
        var staffId = await TechnicianIdAsync(admin, BranchQ3);
        var serviceId = Text(await ActiveServiceAsync(admin), "id")!;

        var first = await TryBookAsync(admin, customerId, staffId, NextSlot(), [serviceId]);

        Assert.Equal(HttpStatusCode.Created, first.Status);

        // Giờ kết thúc lấy từ máy chủ — đã gồm cả thời gian dọn dẹp (BR-APT-010).
        var endAt = first.Body.GetProperty("appointment").GetProperty("endAt").GetDateTimeOffset();

        var next = await TryBookAsync(admin, customerId, staffId, endAt, [serviceId]);

        Assert.Equal(HttpStatusCode.Created, next.Status);
    }

    /// <summary>
    /// BR-APT-010 — khoảng một lịch hẹn chiếm chỗ gồm <b>cả thời gian dọn dẹp</b>, nên một lịch
    /// đặt ngay khi dịch vụ vừa xong vẫn bị chặn.
    /// <para>
    /// Quên phần dọn dẹp là lỗi im lặng đúng nghĩa: hệ thống vẫn nhận lịch, vẫn chống trùng, và
    /// chỉ sai đúng khoảng thời gian mà kỹ thuật viên cần để lau dọn giữa hai khách. Không phép
    /// thử nào ném hai lịch chồng hẳn lên nhau bắt được nó.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Khoang_chiem_cho_gom_ca_thoi_gian_don_dep()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var customerId = await ActiveCustomerIdAsync(admin);
        var staffId = await TechnicianIdAsync(admin, BranchQ3);

        // Cần một dịch vụ CÓ thời gian dọn dẹp: với dịch vụ dọn dẹp 0 phút thì phép thử này
        // trùng nghĩa với phép thử nối đuôi ở trên và không khẳng định được gì.
        var service = await FirstAsync(admin, "/api/services", "services",
            item => item.GetProperty("status").GetString() == "ACTIVE"
                    && item.GetProperty("bufferMinutes").GetInt32() > 0);

        var slot = NextSlot();
        var serviceId = Text(service, "id")!;

        var first = await TryBookAsync(admin, customerId, staffId, slot, [serviceId]);

        Assert.Equal(HttpStatusCode.Created, first.Status);

        // Đúng lúc dịch vụ xong nhưng chưa hết giờ dọn dẹp.
        var tooSoon = slot.AddMinutes(Number(service, "durationMinutes"));

        var clash = await TryBookAsync(admin, customerId, staffId, tooSoon, [serviceId]);

        Assert.Equal(HttpStatusCode.Conflict, clash.Status);
        Assert.Equal("SLOT_CONFLICT", clash.ErrorCode);
    }

    /// <summary>
    /// Luật chống trùng gắn với <b>một con người</b>, không phải với một khung giờ của tiệm:
    /// hai kỹ thuật viên làm cùng lúc là hoạt động bình thường của mọi tiệm nail.
    /// <para>
    /// Vế này cần thiết vì thiếu nó thì một phép kiểm quên mất điều kiện <c>staffId</c> vẫn qua
    /// được cả bộ — và hệ thống sẽ khóa toàn tiệm mỗi khi một người có lịch.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Ky_thuat_vien_khac_van_nhan_duoc_cung_khung_gio()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var customerId = await ActiveCustomerIdAsync(admin);
        var serviceId = Text(await ActiveServiceAsync(admin), "id")!;
        var slot = NextSlot();

        var busy = await TechnicianIdAsync(admin, BranchQ3);
        var free = await FirstIdAsync(admin, "/api/staff", "staff",
            item => item.GetProperty("role").GetString() == "TECHNICIAN"
                    && item.GetProperty("branchId").GetString() == BranchQ3
                    && item.GetProperty("id").GetString() != busy);

        var first = await TryBookAsync(admin, customerId, busy, slot, [serviceId]);
        var second = await TryBookAsync(admin, customerId, free, slot, [serviceId]);

        Assert.Equal(HttpStatusCode.Created, first.Status);
        Assert.Equal(HttpStatusCode.Created, second.Status);
    }

    /// <summary>
    /// BR-APT-012 — lịch đã hủy hoặc khách không đến thì không giữ chỗ của ai nữa.
    /// <para>
    /// Không có luật này thì mỗi lần khách hủy là mất luôn khung giờ đó cho tới hết ngày, và
    /// lễ tân sẽ đi đường vòng: sửa giờ lịch cũ sang một chỗ bịa rồi mới đặt lịch mới — làm
    /// hỏng luôn phần lịch sử mà việc hủy lẽ ra phải giữ lại.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("NO_SHOW")]
    public async Task Lich_khong_con_hieu_luc_thi_tra_lai_khung_gio(string status)
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var customerId = await ActiveCustomerIdAsync(admin);
        var staffId = await TechnicianIdAsync(admin, BranchQ3);
        var serviceId = Text(await ActiveServiceAsync(admin), "id")!;
        var slot = NextSlot();

        var first = await TryBookAsync(admin, customerId, staffId, slot, [serviceId]);

        Assert.Equal(HttpStatusCode.Created, first.Status);

        var appointmentId = Text(first.Body.GetProperty("appointment"), "id")!;

        // Trước khi buông chỗ thì chỗ ấy vẫn phải đang bị giữ — nếu không, phần sau của phép
        // thử chỉ chứng minh rằng một khung giờ trống thì đặt được.
        var whileHeld = await TryBookAsync(admin, customerId, staffId, slot, [serviceId]);

        Assert.Equal(HttpStatusCode.Conflict, whileHeld.Status);

        await AdvanceAsync(admin, appointmentId, status);

        var reused = await TryBookAsync(admin, customerId, staffId, slot, [serviceId]);

        Assert.Equal(HttpStatusCode.Created, reused.Status);
    }

    /// <summary>
    /// Dời một lịch hẹn sang khung giờ chồng lên <b>chính nó</b> vẫn phải chạy — đây là điều
    /// tham số <c>exceptAppointmentId</c> của <c>BlockingSlot</c> đi giữ.
    /// <para>
    /// Thiếu tham số ấy thì thao tác kéo thả một ô trên bảng giờ hỏng theo kiểu khó hiểu nhất:
    /// dời đi xa thì được, nhích mười lăm phút thì bị từ chối vì "đã có lịch" — và cái lịch ấy
    /// chính là cái đang bị kéo.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Doi_gio_khong_lam_lich_hen_tu_bao_trung_voi_chinh_no()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var appointmentId = await BookAsync(admin);
        var before = await admin.GetAsync($"/api/appointments/{appointmentId}");
        var startAt = before.Body.GetProperty("appointment").GetProperty("startAt").GetDateTimeOffset();

        var moved = await admin.PatchAsync(
            $"/api/appointments/{appointmentId}/schedule", new { startAt = startAt.AddMinutes(15) });

        Assert.Equal(HttpStatusCode.OK, moved.Status);
        Assert.Equal(
            startAt.AddMinutes(15),
            moved.Body.GetProperty("appointment").GetProperty("startAt").GetDateTimeOffset());
    }

    /// <summary>
    /// Vế còn lại của phép thử trên: đường dời lịch phải chạy <b>cùng</b> luật chống trùng với
    /// đường đặt mới, chứ không được miễn trừ.
    /// <para>
    /// Đây đúng là chỗ mà <c>AppointmentBookingGuard</c> sinh ra để giữ. Nếu ba đường ghi lịch
    /// hẹn mỗi đường tự kiểm một kiểu thì đường dời giờ là đường dễ bị bỏ sót nhất — nó trông
    /// như chỉ sửa một trường.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Doi_gio_vao_cho_nguoi_khac_dang_giu_thi_van_bi_chan()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var customerId = await ActiveCustomerIdAsync(admin);
        var staffId = await TechnicianIdAsync(admin, BranchQ3);
        var serviceId = Text(await ActiveServiceAsync(admin), "id")!;

        var occupiedSlot = NextSlot();
        var occupied = await TryBookAsync(admin, customerId, staffId, occupiedSlot, [serviceId]);

        Assert.Equal(HttpStatusCode.Created, occupied.Status);

        var mover = await TryBookAsync(admin, customerId, staffId, NextSlot(), [serviceId]);

        Assert.Equal(HttpStatusCode.Created, mover.Status);

        var moved = await admin.PatchAsync(
            $"/api/appointments/{Text(mover.Body.GetProperty("appointment"), "id")}/schedule",
            new { startAt = occupiedSlot });

        Assert.Equal(HttpStatusCode.Conflict, moved.Status);
        Assert.Equal("SLOT_CONFLICT", moved.ErrorCode);
    }
}
