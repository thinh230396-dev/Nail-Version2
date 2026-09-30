using System.Net;
using System.Text.Json;
using NailManagement.Infrastructure.Persistence.Salon.Services;
using NailManagement.Infrastructure.Persistence.Seed;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Scenarios;

/// <summary>
/// Dựng dữ liệu nghiệp vụ cho các phép thử — lịch hẹn, hóa đơn — và những phép đọc phụ trợ
/// mà gần như lớp kiểm thử nào cũng cần.
/// <para>
/// Tách khỏi thư mục <c>Infrastructure</c>: thư mục đó trả lời câu hỏi "làm sao nói chuyện
/// được với máy chủ" (phiên, cookie, database), còn ở đây là "một buổi làm ở tiệm trông như
/// thế nào". Hai trách nhiệm khác nhau, và trộn chúng lại thì thư mục hạ tầng sẽ phình ra
/// theo từng lát cắt nghiệp vụ mới.
/// </para>
/// <para>
/// ⚠️ <b>Lý do thật sự phải dùng chung chứ không chép sang từng lớp: <see cref="NextSlot"/>.</b>
/// BR-APT-011 chặn cứng hai lịch chồng giờ của cùng kỹ thuật viên, và mọi phép thử ở đây đều
/// chọn <b>cùng một</b> kỹ thuật viên đầu danh sách chi nhánh Quận 3. Nếu mỗi lớp giữ bộ đếm
/// riêng thì hai lớp sẽ cùng xin khung giờ thứ nhất, thứ hai, thứ ba… và lớp chạy sau đỏ vì
/// đụng lịch của lớp chạy trước — một phép thử đỏ vì lý do không liên quan gì tới thứ nó kiểm.
/// Bộ đếm phải là <b>một</b>, dùng chung cho cả lần chạy.
/// </para>
/// <para>
/// Dùng qua <c>using static</c> ở từng lớp kiểm thử, để câu lệnh trong thân phép thử đọc lên
/// vẫn gọn như khi các hàm này còn nằm ngay trong lớp.
/// </para>
/// </summary>
public static class SalonScenario
{
    public const string Lumiere = "TEN-LUMIERE";
    public const string Muse = "TEN-MUSE";
    public const string BranchQ3 = "BRN-LUMIERE-Q3";
    public const string BranchQ1 = "BRN-LUMIERE-Q1";

    /// <summary>Bộ đếm khung giờ dùng chung cho cả lần chạy. Xem cảnh báo ở chú thích lớp.</summary>
    private static int _slotCounter;

    /// <summary>
    /// Một khung giờ chưa ai dùng, đặt xa trong tương lai để không đụng lịch của bộ dữ liệu mẫu.
    /// <para>
    /// ⚠️ <b>Khoảng cách phải rộng hơn dịch vụ dài nhất trong danh mục mẫu</b>, chứ không phải
    /// rộng hơn dịch vụ mà <see cref="ActiveServiceAsync"/> tình cờ chọn hôm nay. Bản trước cách
    /// nhau hai tiếng vì tin rằng dịch vụ đầu danh sách luôn ngắn — và điều đó vỡ ngay khi
    /// database đổi collation: <c>ServiceRepository</c> sắp danh mục bằng <c>ThenBy(Name)</c>, tức
    /// sắp <b>trong database</b>, mà tiếng Việt coi "Ch" là chữ cái riêng đứng sau "C". Trên
    /// <c>Vietnamese_CI_AS</c>, "Combo cưới" (200 phút) nhảy lên trước "Chăm sóc da chân"
    /// (55 phút), thế là mọi lịch kế tiếp của cùng kỹ thuật viên chồng giờ nhau và 11 phép thử
    /// đỏ vì BR-APT-011 — không phép thử nào trong số đó kiểm chuyện chống trùng lịch.
    /// </para>
    /// <para>
    /// Tám tiếng vượt xa dịch vụ dài nhất hiện có (Combo cưới: 180 phút + 20 phút dọn dẹp), nên
    /// giả định này không còn phụ thuộc vào thứ tự danh mục nữa. Đánh đổi: các khung giờ trải ra
    /// nhiều ngày thay vì gói trong một ngày — vô hại, vì tất cả vẫn nằm 120 ngày sau.
    /// </para>
    /// <para>
    /// ⚠️ <b>Mốc 120 ngày là khoảng cách với bộ dữ liệu mẫu, không phải một con số tùy ý.</b>
    /// Bộ nạp nay dựng lịch cả ở phía trước — <c>DemoDataSeeder.UpcomingDays</c>, hiện là bảy
    /// ngày — nên hai bên không còn tách nhau bởi ranh giới "mẫu chỉ có quá khứ" như trước. Nới
    /// con số bên ấy tới gần 120 ngày là để dữ liệu mẫu chiếm mất khung giờ của phép thử, và
    /// BR-APT-011 sẽ làm hàng loạt lớp đỏ vì lý do không liên quan tới thứ chúng kiểm. Khoảng
    /// đệm hiện tại là 113 ngày.
    /// </para>
    /// <para>
    /// <c>DateTimeKind.Unspecified</c> rồi bọc bằng <c>TimeSpan.Zero</c>: giờ ghi trên bản ghi là
    /// giờ đồng hồ treo tường của tiệm — BR-EMP-009 so ca theo đúng giờ ấy — nên để
    /// <c>DateTime.UtcNow</c> tự gắn múi giờ của máy chạy test là mời một phép so ca sai ở máy khác.
    /// </para>
    /// </summary>
    public static DateTimeOffset NextSlot()
    {
        var slot = DateTime.SpecifyKind(
            DateTime.UtcNow.Date.AddDays(120).AddHours(8 * Interlocked.Increment(ref _slotCounter)),
            DateTimeKind.Unspecified);

        return new DateTimeOffset(slot, TimeSpan.Zero);
    }

    // ── Lịch hẹn ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Gửi lệnh đặt lịch và trả về <b>nguyên phản hồi</b>, kể cả khi nó là lỗi.
    /// <para>
    /// Có mặt bên cạnh <see cref="BookAsync"/> vì các phép thử chống trùng lịch cần đọc mã lỗi
    /// và câu chữ của một lần đặt <b>bị từ chối</b> — thứ mà một hàm tự khẳng định
    /// <c>Created</c> sẽ nuốt mất.
    /// </para>
    /// </summary>
    public static Task<ApiResponse> TryBookAsync(
        SalonSysClient client,
        string customerId,
        string staffId,
        DateTimeOffset startAt,
        IReadOnlyList<string> serviceIds,
        string status = "CONFIRMED",
        long deposit = 0)
        => client.PostAsync("/api/appointments", new
        {
            customerId,
            staffId,
            startAt,
            services = serviceIds.Select(serviceId => new { serviceId }).ToArray(),
            status,
            deposit
        });

    /// <summary>Lịch hẹn mới ở chi nhánh Quận 3, chiếm một khung giờ chưa ai dùng.</summary>
    public static async Task<string> BookAsync(SalonSysClient client, long deposit = 0)
    {
        var created = await TryBookAsync(
            client,
            await ActiveCustomerIdAsync(client),
            await TechnicianIdAsync(client, BranchQ3),
            NextSlot(),
            [Text(await ActiveServiceAsync(client), "id")!],
            deposit: deposit);

        Assert.Equal(HttpStatusCode.Created, created.Status);

        return created.Body.GetProperty("appointment").GetProperty("id").GetString()!;
    }

    public static async Task<string> AppointmentStatusAsync(SalonSysClient client, string appointmentId)
    {
        var response = await client.GetAsync($"/api/appointments/{appointmentId}");

        Assert.Equal(HttpStatusCode.OK, response.Status);

        return response.Body.GetProperty("appointment").GetProperty("status").GetString()!;
    }

    /// <summary>Đưa một lịch hẹn qua từng bước của sơ đồ §16.1 cho tới trạng thái mong muốn.</summary>
    public static async Task AdvanceAsync(
        SalonSysClient client, string appointmentId, params string[] statuses)
    {
        foreach (var status in statuses)
        {
            var moved = await client.PatchAsync(
                $"/api/appointments/{appointmentId}/status", new { status });

            Assert.Equal(HttpStatusCode.OK, moved.Status);
        }
    }

    // ── Hóa đơn ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Một lịch hẹn đã đi tới trạng thái lập được hóa đơn, kèm hóa đơn của nó — BR-INV-010.
    /// </summary>
    /// <param name="startInService">
    /// Đúng thì dừng ở <c>IN_SERVICE</c>, sai thì dừng ở <c>CHECKED_IN</c>. Cả hai đều lập được
    /// hóa đơn, và sự khác nhau giữa chúng chính là thứ quyết định 57 phải trả lời.
    /// </param>
    public static async Task<(string AppointmentId, JsonElement Invoice)> OpenInvoiceAsync(
        SalonSysClient client, bool startInService, long deposit = 0)
    {
        var appointmentId = await BookAsync(client, deposit);

        await AdvanceAsync(client, appointmentId, "CHECKED_IN");

        if (startInService) await AdvanceAsync(client, appointmentId, "IN_SERVICE");

        var created = await client.PostAsync("/api/sales-invoices", new { appointmentId });

        Assert.Equal(HttpStatusCode.Created, created.Status);

        return (appointmentId, created.Body.GetProperty("invoice"));
    }

    /// <summary>
    /// Hóa đơn bán lẻ với đúng một dòng nhập tay ở mức giá cho trước — BR-INV-011/012.
    /// <para>
    /// Dùng dòng nhập tay chứ không dùng một dịch vụ trong danh mục, để phép thử tự quyết định
    /// số tiền: giá dịch vụ của bộ dữ liệu mẫu đổi lúc nào cũng được, và một phép thử đỏ vì
    /// bảng giá đổi là một phép thử nói dối.
    /// </para>
    /// </summary>
    public static async Task<JsonElement> WalkInInvoiceAsync(SalonSysClient client, long price)
    {
        var created = await client.PostAsync("/api/sales-invoices", new
        {
            customerId = await ActiveCustomerIdAsync(client),
            branchId = BranchQ3,
            lines = new[] { new { name = "Dịch vụ bán lẻ kiểm thử", unitPrice = price, quantity = 1 } }
        });

        Assert.Equal(HttpStatusCode.Created, created.Status);

        return created.Body.GetProperty("invoice");
    }

    // ── Phép đọc phụ trợ ─────────────────────────────────────────────────────

    public static Task<string> TechnicianIdAsync(SalonSysClient client, string branchId)
        => FirstIdAsync(client, "/api/staff", "staff",
            item => item.GetProperty("role").GetString() == "TECHNICIAN"
                    && item.GetProperty("branchId").GetString() == branchId);

    public static Task<string> ActiveCustomerIdAsync(SalonSysClient client)
        => FirstIdAsync(client, "/api/customers", "customers",
            item => item.GetProperty("status").GetString() == "ACTIVE");

    /// <summary>
    /// Trả về cả bản ghi dịch vụ chứ không chỉ mã, vì các phép thử chống trùng lịch phải tính
    /// giờ kết thúc từ <c>durationMinutes</c> và <c>bufferMinutes</c> của <b>chính bản ghi ấy</b>.
    /// Chép cứng con số 60 và 10 vào phép thử là dựng một phép tính thứ hai, và ngày ai đó sửa
    /// bảng giá mẫu thì phép thử đỏ mà không chỉ ra được điều gì sai.
    /// </summary>
    public static Task<JsonElement> ActiveServiceAsync(SalonSysClient client)
        => FirstAsync(client, "/api/services", "services",
            item => item.GetProperty("status").GetString() == "ACTIVE");

    public static async Task<string> FirstIdAsync(
        SalonSysClient client, string path, string arrayProperty, Func<JsonElement, bool> match)
        => (await FirstAsync(client, path, arrayProperty, match)).GetProperty("id").GetString()!;

    public static async Task<JsonElement> FirstAsync(
        SalonSysClient client, string path, string arrayProperty, Func<JsonElement, bool> match)
    {
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.Status);

        var found = response.Body.GetProperty(arrayProperty).EnumerateArray().FirstOrDefault(match);

        Assert.True(
            found.ValueKind == JsonValueKind.Object,
            $"Dữ liệu mẫu phải có ít nhất một bản ghi hợp lệ ở {path}.");

        return found;
    }

    public static string? Text(JsonElement element, string property)
        => element.GetProperty(property).GetString();

    public static long Money(JsonElement element, string property)
        => element.GetProperty(property).GetInt64();

    public static int Number(JsonElement element, string property)
        => element.GetProperty(property).GetInt32();

    /// <summary>Tên các ô nhập bị gắn lỗi trong contract <c>{ error: { code, message, fields } }</c>.</summary>
    public static IReadOnlyList<string?> FieldNames(ApiResponse response)
        => response.Body.TryGetProperty("error", out var error)
           && error.TryGetProperty("fields", out var fields)
           && fields.ValueKind == JsonValueKind.Array
            ? [.. fields.EnumerateArray().Select(field => field.GetProperty("field").GetString())]
            : [];
}
