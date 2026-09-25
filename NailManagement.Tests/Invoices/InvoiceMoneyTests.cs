using System.Net;
using NailManagement.Domain.Shared;
using NailManagement.Tests.Infrastructure;

using static NailManagement.Tests.Scenarios.SalonScenario;

namespace NailManagement.Tests.Invoices;

/// <summary>
/// Công thức tiền và phép suy trạng thái hóa đơn — BR-INV-020/021/022, BR-SVC-006/007,
/// BR-APT-031, BR-PAY-003/008.
/// <para>
/// Hạng mục thứ tư trong bốn thứ mà §6 của lộ trình đánh dấu <b>tuyệt đối không cắt</b>. Lý do
/// khác hẳn lý do của phép chống trùng lịch: một con số tiền sai <b>không bao giờ tự lộ ra</b>.
/// Hóa đơn vẫn in được, khách vẫn trả, và chênh lệch chỉ hiện ra ở cuối tháng khi sổ két và
/// báo cáo doanh thu không khớp nhau — lúc đó không còn cách nào truy ngược từng hóa đơn.
/// </para>
/// <para>
/// Điều những phép thử ở đây đi giữ là <b>máy chủ mới là nơi tính tiền</b>. Cả bốn con số —
/// tiền hàng, tổng phải trả, đã thu, còn lại — đều được khẳng định trên phản hồi của máy chủ
/// chứ không tính lại ở phía kiểm thử, vì cộng lại ở đây là dựng đúng cái phép tính thứ hai
/// mà DTO cố ý sinh ra để tránh.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class InvoiceMoneyTests(SalonSysFactory factory)
{
    /// <summary>
    /// BR-INV-020/021/022 — tổng phải trả bằng <b>tiền hàng − giảm giá + tip</b>, và tiền hàng
    /// là tổng của đơn giá nhân số lượng trên từng dòng.
    /// <para>
    /// Dùng hai dòng với số lượng khác nhau chứ không phải một dòng đơn lẻ: một phép nhân bị bỏ
    /// quên vẫn cho đúng kết quả khi mọi số lượng đều bằng 1, và đó là hình dạng thường gặp
    /// nhất của dữ liệu thử.
    /// </para>
    /// <para>
    /// Tip cộng <b>vào</b> tổng phải trả — BR-INV-022 chỉ loại nó khỏi <i>doanh thu</i>, ở
    /// công thức BR-REV-001, chứ không loại khỏi số tiền khách đưa. Đặt hai luật ấy nhầm chỗ
    /// cho nhau là hoặc khách trả thiếu tiền tip, hoặc tiệm tính tip vào doanh thu của mình.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Tong_phai_tra_bang_tien_hang_tru_giam_gia_cong_tip()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var created = await admin.PostAsync("/api/sales-invoices", new
        {
            customerId = await ActiveCustomerIdAsync(admin),
            branchId = BranchQ3,
            lines = new[]
            {
                new { name = "Sơn gel kiểm thử", unitPrice = 250_000L, quantity = 2 },
                new { name = "Vẽ móng kiểm thử", unitPrice = 180_000L, quantity = 1 }
            },
            discount = 80_000L,
            discountReason = "Khách quen",
            tip = 50_000L
        });

        Assert.Equal(HttpStatusCode.Created, created.Status);

        var invoice = created.Body.GetProperty("invoice");

        Assert.Equal(680_000L, Money(invoice, "subtotal"));   // 250.000×2 + 180.000
        Assert.Equal(80_000L, Money(invoice, "discount"));
        Assert.Equal(50_000L, Money(invoice, "tip"));
        Assert.Equal(650_000L, Money(invoice, "total"));      // 680.000 − 80.000 + 50.000
        Assert.Equal(0L, Money(invoice, "collected"));
        Assert.Equal(650_000L, Money(invoice, "remaining"));
        Assert.Equal("PENDING", Text(invoice, "status"));
        Assert.Equal("Khách quen", Text(invoice, "discountReason"));
    }

    /// <summary>
    /// BR-INV-021 — giảm giá nằm trong khoảng từ 0 tới tổng tiền hàng. Vượt trần thì bị từ
    /// chối ngay, không phải bị kéo về im lặng.
    /// <para>
    /// Ranh giới này giữ cho tổng phải trả không bao giờ âm. Một hóa đơn âm không phải là số
    /// sai vô hại: nó đi thẳng vào công thức doanh thu ở BR-REV-001 và trừ vào doanh thu của
    /// một ngày mà tiệm không hề trả lại đồng nào cho ai.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Giam_gia_vuot_tong_tien_hang_bi_tu_choi()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var refused = await admin.PostAsync("/api/sales-invoices", new
        {
            customerId = await ActiveCustomerIdAsync(admin),
            branchId = BranchQ3,
            lines = new[] { new { name = "Dịch vụ kiểm thử", unitPrice = 200_000L, quantity = 1 } },
            discount = 200_001L
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Equal("VALIDATION_FAILED", refused.ErrorCode);
        Assert.Contains("discount", FieldNames(refused));
    }

    /// <summary>
    /// Đầu dưới của cùng khoảng ấy: "giảm giá −500.000" là phép <b>cộng</b> tiền vào hóa đơn
    /// bằng cửa sau, nên nó phải bị chặn y như phép vượt trần.
    /// <para>
    /// Đường <b>sửa</b> hóa đơn luôn cưỡng chế đúng luật này vì nó gọi thẳng
    /// <c>ApplyDiscount</c> không kèm điều kiện nào. Đường <b>lập mới</b> thì từng bỏ sót — xem
    /// <see cref="Giam_gia_am_bi_tu_choi_khi_lap_hoa_don"/>, phép thử ra đời cùng lần vá.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Giam_gia_am_bi_tu_choi_khi_sua_hoa_don()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var invoice = await WalkInInvoiceAsync(admin, 200_000L);

        var refused = await admin.PutAsync($"/api/sales-invoices/{Text(invoice, "id")}", new
        {
            lines = new[] { new { name = "Dịch vụ kiểm thử", unitPrice = 200_000L, quantity = 1 } },
            discount = -50_000L,
            tip = 0L
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Equal("VALIDATION_FAILED", refused.ErrorCode);
        Assert.Contains("discount", FieldNames(refused));
    }

    /// <summary>
    /// Cùng luật ấy ở đường <b>lập</b> hóa đơn — chỗ từng nuốt con số âm trong im lặng.
    /// <para>
    /// <c>CreateSalesInvoiceUseCase</c> trước đây chỉ gọi <c>ApplyDiscount</c> khi số tiền giảm
    /// <b>lớn hơn 0</b>, nên một con số âm không bao giờ tới được nơi duy nhất biết từ chối nó.
    /// Máy chủ trả <c>201</c> với hóa đơn giảm giá 0đ: tiền trên hóa đơn vẫn đúng, nhưng người
    /// gửi <c>discount: -50000</c> không có cách nào biết yêu cầu của mình đã bị bỏ qua. Một
    /// giá trị sai bị nuốt lặng lẽ tệ hơn một giá trị sai bị từ chối, vì người dùng tin là nó
    /// đã được áp dụng.
    /// </para>
    /// <para>
    /// Phép thử này và <see cref="Tip_am_bi_tu_choi_khi_lap_hoa_don"/> là hai nửa của cùng một
    /// lỗi: điều kiện <c>&gt; 0</c> che cả hai trường.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Giam_gia_am_bi_tu_choi_khi_lap_hoa_don()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var refused = await admin.PostAsync("/api/sales-invoices", new
        {
            customerId = await ActiveCustomerIdAsync(admin),
            branchId = BranchQ3,
            lines = new[] { new { name = "Dịch vụ kiểm thử", unitPrice = 200_000L, quantity = 1 } },
            discount = -50_000L
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Equal("VALIDATION_FAILED", refused.ErrorCode);
        Assert.Contains("discount", FieldNames(refused));
    }

    /// <summary>
    /// Tip âm cũng bị từ chối khi lập hóa đơn — <c>Guard.Money</c> chặn, miễn là lời gọi tới
    /// được nó.
    /// </summary>
    [Fact]
    public async Task Tip_am_bi_tu_choi_khi_lap_hoa_don()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var refused = await admin.PostAsync("/api/sales-invoices", new
        {
            customerId = await ActiveCustomerIdAsync(admin),
            branchId = BranchQ3,
            lines = new[] { new { name = "Dịch vụ kiểm thử", unitPrice = 200_000L, quantity = 1 } },
            tip = -20_000L
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Contains("tip", FieldNames(refused));
    }

    /// <summary>
    /// Ranh giới của lần vá: <b>số 0 vẫn phải đi lọt</b>.
    /// <para>
    /// <c>discount</c> và <c>tip</c> là hai trường <c>long</c> không nullable, nên "khách không
    /// gửi gì" và "khách gửi số 0" là cùng một giá trị. Nếu lần vá đổi điều kiện thành "luôn
    /// gọi" thay vì "gọi khi khác 0", mọi hóa đơn lập không kèm giảm giá sẽ mang một
    /// <c>discountReason</c> được ghi vào dù không hề được giảm giá — và phép thử này là thứ
    /// bắt được điều đó.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Khong_gui_giam_gia_thi_hoa_don_van_lap_binh_thuong()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var created = await admin.PostAsync("/api/sales-invoices", new
        {
            customerId = await ActiveCustomerIdAsync(admin),
            branchId = BranchQ3,
            lines = new[] { new { name = "Dịch vụ kiểm thử", unitPrice = 200_000L, quantity = 1 } }
        });

        Assert.Equal(HttpStatusCode.Created, created.Status);

        var invoice = created.Body.GetProperty("invoice");

        Assert.Equal(0L, Money(invoice, "discount"));
        Assert.Equal(0L, Money(invoice, "tip"));
        Assert.Equal(200_000L, Money(invoice, "total"));
        Assert.True(string.IsNullOrEmpty(Text(invoice, "discountReason")));
    }

    /// <summary>
    /// BR-SVC-007 — giá của một dịch vụ <b>có trong danh mục</b> luôn lấy từ máy chủ, kể cả khi
    /// client gửi kèm một con số khác.
    /// <para>
    /// Đây là ranh giới giữa hai nhánh của cùng một ô nhập: dòng gắn dịch vụ thì máy chủ chốt
    /// giá, dòng nhập tay thì client chốt giá (BR-INV-012) vì dòng ấy không tham chiếu bảng nào
    /// để mà đối chiếu. Phép thử gửi lên một mức giá <b>vô lý</b> chứ không phải một mức giá
    /// gần đúng, để nếu luật hỏng thì con số hiện ra là con số không thể nhầm với giá thật.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Gia_dich_vu_trong_danh_muc_lay_tu_may_chu_khong_lay_tu_client()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var service = await ActiveServiceAsync(admin);
        var cataloguePrice = Money(service, "price");

        var created = await admin.PostAsync("/api/sales-invoices", new
        {
            customerId = await ActiveCustomerIdAsync(admin),
            branchId = BranchQ3,
            lines = new[]
            {
                new { serviceId = Text(service, "id"), name = "Tên do client tự đặt", unitPrice = 1_000L, quantity = 1 }
            }
        });

        Assert.Equal(HttpStatusCode.Created, created.Status);

        var line = created.Body.GetProperty("invoice").GetProperty("lines").EnumerateArray().Single();

        Assert.Equal(cataloguePrice, Money(line, "unitPrice"));
        Assert.Equal(Text(service, "name"), Text(line, "name"));
        Assert.Equal(cataloguePrice, Money(created.Body.GetProperty("invoice"), "subtotal"));
    }

    /// <summary>
    /// BR-APT-031 — tiền cọc của lịch hẹn trở thành một <b>dòng thu</b> ngay lúc lập hóa đơn,
    /// chứ không phải một ô số dư trên hóa đơn.
    /// <para>
    /// Là một dòng thu nên nó tự chảy vào mọi phép cộng đã có: tổng đã thu, phép suy trạng thái
    /// ở BR-PAY-003, và công thức doanh thu ở BR-REV-001. Là một ô riêng thì cả ba chỗ ấy đều
    /// phải nhớ cộng thêm nó — và chỗ nào quên thì tiền cọc của khách biến mất khỏi sổ.
    /// </para>
    /// <para>
    /// Loại dòng phải là <c>DEPOSIT</c> chứ không phải <c>PAYMENT</c>: hai khoản này khác nhau
    /// ở thời điểm và ở việc khách đã được phục vụ hay chưa, và chỉ có cột loại mới phân biệt
    /// được chúng về sau.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Tien_coc_cua_lich_hen_thanh_mot_dong_thu_ngay_luc_lap_hoa_don()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var (_, invoice) = await OpenInvoiceAsync(admin, startInService: true, deposit: 100_000L);

        var deposit = invoice.GetProperty("payments").EnumerateArray().Single();

        Assert.Equal("DEPOSIT", Text(deposit, "type"));
        Assert.Equal(100_000L, Money(deposit, "amount"));
        Assert.Equal(100_000L, Money(invoice, "collected"));

        // Đã thu một phần nhưng chưa đủ — trạng thái phải nói đúng điều đó, và số còn lại phải
        // là phần khách còn nợ chứ không phải toàn bộ tổng phải trả.
        Assert.Equal("PARTIAL", Text(invoice, "status"));
        Assert.Equal(Money(invoice, "total") - 100_000L, Money(invoice, "remaining"));
    }

    /// <summary>
    /// BR-PAY-003 — ba trạng thái <c>PENDING</c>, <c>PARTIAL</c>, <c>PAID</c> là <b>kết quả</b>
    /// của tổng thu, không phải thứ ai chọn; <c>REFUNDED</c> thì phải đi kèm số tiền và lý do
    /// nên nó có đường riêng.
    /// <para>
    /// Nhận chúng từ client là cho phép đánh dấu một hóa đơn chưa thu đồng nào thành đã thanh
    /// toán — và vì <b>lịch hẹn tự hoàn tất</b> khi hóa đơn chuyển sang <c>PAID</c> (BR-APT-026),
    /// một lỗ ở đây không dừng lại ở sổ tiền mà đóng luôn cả buổi làm.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("PENDING")]
    [InlineData("PARTIAL")]
    [InlineData("PAID")]
    [InlineData("REFUNDED")]
    public async Task Trang_thai_hoa_don_khong_dat_tay_duoc(string status)
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var invoice = await WalkInInvoiceAsync(admin, 300_000L);

        var refused = await admin.PatchAsync(
            $"/api/sales-invoices/{Text(invoice, "id")}/status", new { status });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.Status);
        Assert.Equal("VALIDATION_FAILED", refused.ErrorCode);

        // Bị từ chối nghĩa là hóa đơn còn nguyên. Một lỗi ném ra SAU khi bản ghi đã đổi vẫn
        // trả về đúng mã lỗi này, nên chỉ nhìn mã thì không phân biệt được.
        var after = await admin.GetAsync($"/api/sales-invoices/{Text(invoice, "id")}");

        Assert.Equal("PENDING", Text(after.Body.GetProperty("invoice"), "status"));
    }

    /// <summary>
    /// BR-PAY-008 — trần của tổng hoàn là tổng đã thu, và <b>đúng bằng trần thì vẫn được</b>.
    /// <para>
    /// Phép thử ở lát cắt thu tiền đã khẳng định vế vượt trần một đồng bị chặn; vế này là nửa
    /// còn lại của cùng một ranh giới. Thiếu nó thì một phép so <c>&gt;=</c> viết nhầm chỗ
    /// <c>&gt;</c> vẫn xanh, và tiệm mất khả năng hoàn trọn tiền cho một khách khiếu nại — đúng
    /// tình huống mà việc hoàn tiền sinh ra để xử lý.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Hoan_dung_bang_tong_da_thu_thi_van_duoc()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var invoice = await WalkInInvoiceAsync(admin, 450_000L);
        var invoiceId = Text(invoice, "id");

        await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/payments", new { method = "CASH", amount = 450_000L });

        var refunded = await admin.PostAsync(
            $"/api/sales-invoices/{invoiceId}/refunds",
            new { method = "CASH", amount = 450_000L, reason = "Khách hủy toàn bộ dịch vụ" });

        Assert.Equal(HttpStatusCode.Created, refunded.Status);

        var body = refunded.Body.GetProperty("invoice");

        Assert.Equal("REFUNDED", Text(body, "status"));

        // Dòng âm cân bằng đúng dòng dương, nên két về 0 — và công thức doanh thu ở BR-REV-001
        // chỉ cần cộng dồn là đã tự trừ hết phần đã trả lại.
        Assert.Equal(0L, Money(body, "collected"));
    }

    /// <summary>
    /// BR-INV-015 — sửa trọn một hóa đơn chưa thu đủ thì mọi con số tiền phải được tính lại,
    /// không phải chỉ những con số vừa gửi lên.
    /// <para>
    /// Đây là đường dễ để lại số cũ nhất: lệnh sửa thay cả bộ dòng, và nếu tổng tiền hàng không
    /// được tính lại thì hóa đơn mang một tổng không khớp với chính các dòng của nó — thứ mà
    /// người ở quầy phát hiện bằng cách cộng tay trước mặt khách.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Sua_bo_dong_thi_moi_con_so_tien_deu_duoc_tinh_lai()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var invoice = await WalkInInvoiceAsync(admin, 500_000L);
        var invoiceId = Text(invoice, "id");

        Assert.Equal(500_000L, Money(invoice, "subtotal"));

        var updated = await admin.PutAsync($"/api/sales-invoices/{invoiceId}", new
        {
            lines = new[]
            {
                new { name = "Dòng thay thế", unitPrice = 120_000L, quantity = 3 }
            },
            discount = 60_000L,
            discountReason = "Điều chỉnh sau khi sửa dòng",
            tip = 0L
        });

        Assert.Equal(HttpStatusCode.OK, updated.Status);

        var body = updated.Body.GetProperty("invoice");

        Assert.Equal(360_000L, Money(body, "subtotal"));
        Assert.Equal(300_000L, Money(body, "total"));
        Assert.Equal(300_000L, Money(body, "remaining"));
        Assert.Single(body.GetProperty("lines").EnumerateArray());
    }

    /// <summary>
    /// BR-INV-016 — số hóa đơn theo khuôn <c>HD-yyyyMMdd-nnn</c>, đánh riêng theo từng tiệm và
    /// đặt lại mỗi ngày.
    /// <para>
    /// Kiểm ở đây chứ không ở lát cắt thu tiền, vì con số ấy là <b>danh tính</b> của chứng từ
    /// mang mọi con số tiền phía trên: hai hóa đơn cùng số là hai chứng từ không phân biệt được,
    /// và cả sổ sách lẫn đường tra cứu của khách đều dựa vào nó.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Moi_hoa_don_mang_mot_so_rieng_theo_khuon_da_chot()
    {
        using var admin = await SalonSysClient.TenantAdminAsync(factory, Lumiere);

        var first = Text(await WalkInInvoiceAsync(admin, 100_000L), "code")!;
        var second = Text(await WalkInInvoiceAsync(admin, 100_000L), "code")!;

        Assert.NotEqual(first, second);
        Assert.Matches(@"^HD-\d{8}-\d{3,}$", first);
        Assert.Matches(@"^HD-\d{8}-\d{3,}$", second);
    }

}
