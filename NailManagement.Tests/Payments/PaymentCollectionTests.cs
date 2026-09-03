using System.Net;
using NailManagement.Tests.Infrastructure;
using static NailManagement.Tests.Scenarios.SalonScenario;

namespace NailManagement.Tests.Payments;

/// <summary>
/// Lát cắt thu tiền — BR-PAY-001…008, BR-APT-026/027.
/// <para>
/// Đây là chỗ <b>ba bảng phải cùng đổi trong một giao dịch</b>: dòng thu tiền, hóa đơn, và lịch
/// hẹn. Rủi ro số 3 ở §7 của lộ trình chỉ đúng chỗ này, và §6 xếp "công thức tiền và phép suy
/// trạng thái hóa đơn" vào bốn hạng mục tuyệt đối không cắt.
/// </para>
/// <para>
/// Mọi phép thử ở đây <b>tự dựng dữ liệu của mình</b> — lịch hẹn, hóa đơn — thay vì mượn bản ghi
/// của bộ dữ liệu mẫu. Thu tiền là thao tác một chiều: hóa đơn đã thanh toán thì không sửa và
/// không hủy được nữa (BR-INV-014). Mượn bản ghi mẫu là để lần chạy thứ hai gặp một hóa đơn đã
/// đóng và đỏ vì lý do không liên quan gì tới thứ đang kiểm.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class PaymentCollectionTests(SalonSysFactory factory)
{
    // ── BR-PAY-003 · BR-APT-026 ──────────────────────────────────────────────

    /// <summary>
    /// Thu làm hai lần: lần đầu hóa đơn thành <c>PARTIAL</c> và lịch hẹn <b>chưa</b> đóng, lần
    /// sau thành <c>PAID</c> và lịch hẹn tự hoàn tất.
    /// <para>
    /// Kiểm cả hai bước trong một phép thử là có chủ đích: điều đáng khẳng định không phải
    /// "trả đủ thì xong", mà là <b>ranh giới</b> giữa hai bước — một hệ thống đóng lịch ngay ở
    /// lần thu đầu tiên vẫn qua được một phép thử chỉ nhìn trạng thái cuối.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Thu_lam_hai_lan_thi_hoa_don_qua_PARTIAL_roi_moi_PAID_va_lich_hen_tu_hoan_tat()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var (appointmentId, invoice) = await OpenInvoiceAsync(admin, startInService: true);
        var invoiceId = Text(invoice, "id");
        var total = Money(invoice, "total");

        Assert.Equal("PENDING", Text(invoice, "status"));

        var half = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/payments",
            new { method = "CASH", amount = total / 2 });

        Assert.Equal(HttpStatusCode.Created, half.Status);

        var partial = half.Body.GetProperty("invoice");

        Assert.Equal("PARTIAL", Text(partial, "status"));
        Assert.Equal(total - total / 2, Money(partial, "remaining"));
        Assert.Equal("IN_SERVICE", await AppointmentStatusAsync(admin, appointmentId));

        var rest = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/payments",
            new { method = "BANK", amount = total - total / 2, reference = "FT26082800123" });

        Assert.Equal(HttpStatusCode.Created, rest.Status);

        var paid = rest.Body.GetProperty("invoice");

        Assert.Equal("PAID", Text(paid, "status"));
        Assert.Equal(0L, Money(paid, "remaining"));

        // BR-PAY-004 — hai phương thức là hai dòng, không phải một trường trên hóa đơn.
        var methods = paid.GetProperty("payments").EnumerateArray()
            .Select(payment => payment.GetProperty("method").GetString()).ToList();

        Assert.Equal(["CASH", "BANK"], methods);

        // BR-APT-026 — đây là điều duy nhất đóng được một lịch hẹn theo đường thường.
        Assert.Equal("COMPLETED", await AppointmentStatusAsync(admin, appointmentId));
    }

    /// <summary>
    /// Quyết định 57 — khách check-in rồi trả đủ tiền luôn, chưa kịp chuyển sang "đang phục vụ".
    /// <para>
    /// Sơ đồ mục 16.1 chỉ vẽ mũi tên tới <c>COMPLETED</c> từ <c>IN_SERVICE</c>, nhưng BR-INV-010
    /// lại cho lập hóa đơn từ cả lịch đang <c>CHECKED_IN</c>. Nếu đường thu tiền bám nguyên sơ đồ
    /// thì lần thu này <b>thất bại</b> — hệ thống từ chối tiền thật của khách vì một mũi tên
    /// thiếu trong tài liệu.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Khach_tra_du_khi_moi_check_in_thi_van_thu_duoc_va_lich_hen_dong_lai()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var (appointmentId, invoice) = await OpenInvoiceAsync(admin, startInService: false);

        Assert.Equal("CHECKED_IN", await AppointmentStatusAsync(admin, appointmentId));

        var paid = await admin.PostAsync(
            $"/api/sales-invoices/{Text(invoice, "id")}/payments",
            new { method = "CASH", amount = Money(invoice, "total") });

        Assert.Equal(HttpStatusCode.Created, paid.Status);
        Assert.Equal("PAID", Text(paid.Body.GetProperty("invoice"), "status"));
        Assert.Equal("COMPLETED", await AppointmentStatusAsync(admin, appointmentId));
    }

    /// <summary>
    /// BR-PAY-003 — trạng thái suy ra từ tổng thu, nên hóa đơn bán lẻ không gắn lịch hẹn nào
    /// vẫn thu bình thường và không có gì để đóng.
    /// </summary>
    [Fact]
    public async Task Hoa_don_ban_le_khong_co_lich_hen_thi_thu_tien_van_chay()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var invoice = await WalkInInvoiceAsync(admin, 150_000L);

        var paid = await admin.PostAsync(
            $"/api/sales-invoices/{Text(invoice, "id")}/payments",
            new { method = "MOMO", amount = 150_000L });

        Assert.Equal(HttpStatusCode.Created, paid.Status);

        var body = paid.Body.GetProperty("invoice");

        Assert.Equal("PAID", Text(body, "status"));
        Assert.Null(Text(body, "appointmentId"));
    }

    // ── Đầu vào ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Ba đầu vào sai, ba câu trả lời gắn đúng ô nhập. Contract lỗi có trường <c>fields</c>
    /// chính là để biểu mẫu ở quầy bôi đúng ô chứ không nổi lên một thông báo chung.
    /// </summary>
    [Theory]
    [InlineData("PAYPAL", 100_000L, "method")]
    [InlineData("CASH", 0L, "amount")]
    [InlineData("CASH", -50_000L, "amount")]
    public async Task Dau_vao_sai_bi_tu_choi_kem_ten_o_nhap(string method, long amount, string field)
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var invoice = await WalkInInvoiceAsync(admin, 200_000L);

        var response = await admin.PostAsync(
            $"/api/sales-invoices/{Text(invoice, "id")}/payments", new { method, amount });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.Equal("VALIDATION_FAILED", response.ErrorCode);
        Assert.Contains(field, FieldNames(response));
    }

    /// <summary>
    /// BR-INV-015 — hóa đơn đã hủy thì không thu thêm được. Câu chữ phải nói ra trạng thái, vì
    /// người ở quầy đang nhìn một màn hình mở từ trước lúc ai đó bấm hủy.
    /// </summary>
    [Fact]
    public async Task Hoa_don_da_huy_thi_khong_thu_them_duoc()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var invoice = await WalkInInvoiceAsync(admin, 300_000L);
        var invoiceId = Text(invoice, "id");

        await admin.PatchAsync($"/api/sales-invoices/{invoiceId}/status", new { status = "CANCELLED" });

        var response = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/payments", new { method = "CASH", amount = 300_000L });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.Equal("VALIDATION_FAILED", response.ErrorCode);
    }

    // ── BR-PAY-006/007/008 — hoàn tiền ───────────────────────────────────────

    /// <summary>
    /// BR-PAY-007 — lễ tân thu tiền cả ngày nhưng không trả tiền ra khỏi két được.
    /// <para>
    /// Đây là endpoint <b>duy nhất</b> trên tài nguyên hóa đơn mà lễ tân nhận 403, nên phép thử
    /// khẳng định luôn vế còn lại: cùng người đó vừa thu tiền thành công trên chính hóa đơn ấy.
    /// Thiếu vế này thì một lỗi khóa nhầm cả nhóm hóa đơn vẫn qua được.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Le_tan_thu_duoc_tien_nhung_khong_hoan_duoc()
    {
        using var receptionist = await SalonSysClient.ReceptionistAsync(factory);

        var invoice = await WalkInInvoiceAsync(receptionist, 400_000L);
        var invoiceId = Text(invoice, "id");

        var collected = await receptionist.PostAsync(
            $"/api/sales-invoices/{invoiceId}/payments", new { method = "CASH", amount = 400_000L });

        Assert.Equal(HttpStatusCode.Created, collected.Status);

        var refused = await receptionist.PostAsync(
            $"/api/sales-invoices/{invoiceId}/refunds",
            new { method = "CASH", amount = 100_000L, reason = "Khách không hài lòng" });

        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        Assert.Equal("FORBIDDEN", refused.ErrorCode);
    }

    /// <summary>
    /// BR-PAY-006 — hoàn tiền là một dòng mang số <b>âm</b> kèm lý do, không phải sửa hay xóa
    /// dòng thu cũ. Phép thử khẳng định thẳng vào dấu âm vì chính nó là thứ khiến công thức
    /// doanh thu ở BR-REV-001 chỉ cần cộng dồn là đã tự trừ phần đã trả lại.
    /// </summary>
    [Fact]
    public async Task Chu_tiem_hoan_tien_sinh_mot_dong_am_va_hoa_don_thanh_REFUNDED()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var invoice = await WalkInInvoiceAsync(admin, 500_000L);
        var invoiceId = Text(invoice, "id");

        await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/payments", new { method = "CASH", amount = 500_000L });

        var refunded = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/refunds",
            new { method = "CASH", amount = 200_000L, reason = "Khách phàn nàn màu sơn bị lệch" });

        Assert.Equal(HttpStatusCode.Created, refunded.Status);

        var body = refunded.Body.GetProperty("invoice");

        Assert.Equal("REFUNDED", Text(body, "status"));
        Assert.Equal(300_000L, Money(body, "collected"));

        var refund = body.GetProperty("payments").EnumerateArray()
            .Single(payment => payment.GetProperty("type").GetString() == "REFUND");

        Assert.Equal(-200_000L, refund.GetProperty("amount").GetInt64());
        Assert.Equal("Khách phàn nàn màu sơn bị lệch", refund.GetProperty("reason").GetString());
    }

    /// <summary>
    /// Ba đường hoàn tiền bị chặn, mỗi đường một luật:
    /// <list type="bullet">
    ///   <item>Hóa đơn chưa thu đủ — sơ đồ mục 16.2 chỉ có mũi tên <c>PAID → REFUNDED</c></item>
    ///   <item>BR-PAY-008 — tổng hoàn không vượt tổng đã thu</item>
    ///   <item>BR-PAY-006 — thiếu lý do thì không có gì giải thích được vì sao két thiếu tiền</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Ba_duong_hoan_tien_khong_hop_le_deu_bi_chan()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var invoice = await WalkInInvoiceAsync(admin, 600_000L);
        var invoiceId = Text(invoice, "id");

        var tooEarly = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/refunds",
            new { method = "CASH", amount = 100_000L, reason = "Chưa thu đồng nào" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooEarly.Status);
        Assert.Contains("status", FieldNames(tooEarly));

        await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/payments", new { method = "CASH", amount = 600_000L });

        var tooMuch = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/refunds",
            new { method = "CASH", amount = 600_001L, reason = "Hoàn quá số đã thu" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMuch.Status);
        Assert.Contains("amount", FieldNames(tooMuch));

        var noReason = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/refunds", new { method = "CASH", amount = 100_000L });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, noReason.Status);
        Assert.Contains("reason", FieldNames(noReason));
    }

    /// <summary>
    /// Mục 16.2 — <c>REFUNDED</c> là điểm cuối, nên mỗi hóa đơn chỉ hoàn được một lần. Đây là
    /// chủ đích của sơ đồ chứ không phải thiếu sót: chứng từ đã đóng thì mọi điều chỉnh sau đó
    /// thuộc về sổ sách bên ngoài phần mềm.
    /// </summary>
    [Fact]
    public async Task Hoa_don_da_hoan_thi_khong_hoan_lan_thu_hai()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var invoice = await WalkInInvoiceAsync(admin, 700_000L);
        var invoiceId = Text(invoice, "id");

        await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/payments", new { method = "CASH", amount = 700_000L });

        await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/refunds",
            new { method = "CASH", amount = 100_000L, reason = "Hoàn lần một" });

        var second = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/refunds",
            new { method = "CASH", amount = 100_000L, reason = "Hoàn lần hai" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.Status);
    }

    // ── BR-APT-027 — chủ tiệm đóng lịch khi chưa thu đủ ──────────────────────

    /// <summary>
    /// BR-APT-027 — ngoại lệ chỉ chủ tiệm có. Lễ tân gửi cùng một thân request lên cùng một
    /// đường dẫn thì nhận 403.
    /// <para>
    /// Phép thử đi qua HTTP nên nó khẳng định luôn một điều mà gọi thẳng use case không thấy
    /// được: cùng endpoint ấy vẫn là đường hủy lịch mà lễ tân dùng cả ngày, nên phép kiểm quyền
    /// phải phân biệt theo <b>nội dung</b> request chứ không khóa cả đường dẫn.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Le_tan_khong_dong_duoc_lich_hen_chua_thu_du()
    {
        using var receptionist = await SalonSysClient.ReceptionistAsync(factory);

        var (appointmentId, _) = await OpenInvoiceAsync(receptionist, startInService: true);

        var refused = await receptionist.PatchAsync(
            $"/api/appointments/{appointmentId}/status", new { status = "COMPLETED" });

        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        Assert.Equal("FORBIDDEN", refused.ErrorCode);

        // Cùng đường dẫn, cùng người: đường đổi trạng thái vẫn phải chạy. Thiếu vế này thì một
        // lần khóa nhầm cả endpoint vẫn qua được phép thử. BR-APT-040 — lịch đang phục vụ không
        // hủy được, nên câu trả lời đúng ở đây là 422 của luật vòng đời, KHÔNG phải 403 của
        // phân quyền.
        var cancelled = await receptionist.PatchAsync(
            $"/api/appointments/{appointmentId}/status", new { status = "CANCELLED" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancelled.Status);
        Assert.Equal("VALIDATION_FAILED", cancelled.ErrorCode);
    }

    /// <summary>
    /// BR-APT-027 — chủ tiệm đóng được lịch khi hóa đơn còn <c>PARTIAL</c>, và hệ thống "ghi
    /// chú hoàn tất khi chưa thu đủ" bằng cờ <c>completedWithUnpaidBalance</c> đi thẳng ra DTO.
    /// </summary>
    [Fact]
    public async Task Chu_tiem_dong_duoc_lich_hen_con_thieu_tien_va_he_thong_ghi_chu_lai()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var (appointmentId, invoice) = await OpenInvoiceAsync(admin, startInService: true);

        await admin.PostAsync(
            $"/api/sales-invoices/{Text(invoice, "id")}/payments",
            new { method = "CASH", amount = 50_000L });

        var completed = await admin.PatchAsync(
            $"/api/appointments/{appointmentId}/status", new { status = "COMPLETED" });

        Assert.Equal(HttpStatusCode.OK, completed.Status);

        var body = completed.Body.GetProperty("appointment");

        Assert.Equal("COMPLETED", Text(body, "status"));
        Assert.True(body.GetProperty("completedWithUnpaidBalance").GetBoolean());
    }

    /// <summary>
    /// Quyết định 58 — lịch <b>chưa có hóa đơn nào</b> vẫn đóng được. Đây là ca tệ hơn hẳn ca
    /// mà BR-APT-027 mô tả: khách bỏ về giữa chừng không trả đồng nào, và BR-APT-040 lại cấm hủy
    /// một lịch đang phục vụ. Không có đường này thì lịch ấy kẹt vĩnh viễn trên bảng lịch.
    /// </summary>
    [Fact]
    public async Task Lich_hen_chua_co_hoa_don_van_dong_tay_duoc()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var appointmentId = await BookAsync(admin);

        await admin.PatchAsync($"/api/appointments/{appointmentId}/status", new { status = "CHECKED_IN" });
        await admin.PatchAsync($"/api/appointments/{appointmentId}/status", new { status = "IN_SERVICE" });

        var completed = await admin.PatchAsync(
            $"/api/appointments/{appointmentId}/status", new { status = "COMPLETED" });

        Assert.Equal(HttpStatusCode.OK, completed.Status);
        Assert.True(completed.Body.GetProperty("appointment")
            .GetProperty("completedWithUnpaidBalance").GetBoolean());
    }

    /// <summary>
    /// BR-APT-027 chỉ nói tới lịch <b>đang được phục vụ</b>. Một lịch mới check-in thì chưa ai
    /// đụng vào khách, nên đường đúng của nó là chuyển sang "đang phục vụ" như mọi ngày — kể cả
    /// với chủ tiệm.
    /// </summary>
    [Fact]
    public async Task Chu_tiem_khong_dong_tat_duoc_lich_moi_check_in()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var appointmentId = await BookAsync(admin);

        await admin.PatchAsync($"/api/appointments/{appointmentId}/status", new { status = "CHECKED_IN" });

        var refused = await admin.PatchAsync(
            $"/api/appointments/{appointmentId}/status", new { status = "COMPLETED" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Equal("VALIDATION_FAILED", refused.ErrorCode);
    }

    // ── Thu không vượt số còn thiếu ──────────────────────────────────────────

    /// <summary>
    /// Thu quá số còn thiếu bị chặn ở <b>cả hai đường</b>: vượt ngay lần đầu, và thu thêm lên
    /// một hóa đơn đã trả đủ.
    /// <para>
    /// Vế thứ hai mới là vế dễ mất. Một phép kiểm chỉ so với tổng hóa đơn vẫn chặn được lần
    /// đầu, nhưng để lọt mọi lần thu sau đó — và đó chính là đường mà buổi tổng duyệt ngày 19
    /// đi vào: hóa đơn đã <c>PAID</c>, thu thêm 1.000₫ vẫn nhận, <c>remaining</c> thành âm.
    /// </para>
    /// <para>
    /// Đối xứng với <c>Ba_duong_hoan_tien_khong_hop_le_deu_bi_chan</c>: trước ngày 19 chỉ chiều
    /// hoàn tiền có trần, còn chiều thu vào thì không. Phép thử khẳng định thêm rằng
    /// <b>thu đúng bằng số còn thiếu vẫn được</b> — thiếu vế ấy thì một dấu <c>&gt;=</c> viết
    /// nhầm chỗ <c>&gt;</c> vẫn xanh, và tiệm mất khả năng thu nốt đồng cuối cùng.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Thu_qua_so_con_thieu_bi_chan_ca_lan_dau_lan_hoa_don_da_du()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var invoice = await WalkInInvoiceAsync(admin, 500_000L);
        var invoiceId = Text(invoice, "id");
        var total = Money(invoice, "total");

        var tooMuch = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/payments",
            new { method = "CASH", amount = total + 1 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMuch.Status);
        Assert.Equal("VALIDATION_FAILED", tooMuch.ErrorCode);
        Assert.Contains("amount", FieldNames(tooMuch));

        // Lần từ chối KHÔNG được để lại dấu vết: một lỗi ném ra sau khi đã ghi vẫn trả về đúng
        // mã lỗi ấy, nên chỉ nhìn mã thì không phân biệt được.
        var untouched = await admin.GetAsync($"/api/sales-invoices/{invoiceId}");

        Assert.Equal("PENDING", Text(untouched.Body.GetProperty("invoice"), "status"));
        Assert.Equal(total, Money(untouched.Body.GetProperty("invoice"), "remaining"));

        var exact = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/payments", new { method = "CASH", amount = total });

        Assert.Equal(HttpStatusCode.Created, exact.Status);
        Assert.Equal("PAID", Text(exact.Body.GetProperty("invoice"), "status"));
        Assert.Equal(0L, Money(exact.Body.GetProperty("invoice"), "remaining"));

        var afterPaid = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/payments", new { method = "CASH", amount = 1_000L });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, afterPaid.Status);
        Assert.Contains("amount", FieldNames(afterPaid));

        var stillZero = await admin.GetAsync($"/api/sales-invoices/{invoiceId}");

        Assert.Equal(0L, Money(stillZero.Body.GetProperty("invoice"), "remaining"));
    }
}
