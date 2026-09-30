using System.Net;
using NailManagement.Tests.Infrastructure;

using static NailManagement.Tests.Scenarios.SalonScenario;

namespace NailManagement.Tests.Concurrency;

/// <summary>
/// BR-INV-016 — số hóa đơn HD-yyyyMMdd-nnn cấp theo tiệm và theo ngày, <b>kể cả khi nhiều quầy
/// lập hóa đơn cùng một lúc</b>.
/// <para>
/// Bộ đếm từng đọc số cuối, cộng một trong bộ nhớ rồi ghi lại. Hai request đọc cùng lúc thì cùng
/// ra một số; chỉ mục duy nhất trên (tiệm, số hóa đơn) chặn được bản trùng nhưng request thứ hai
/// nhận HTTP 500 — lễ tân thấy "máy chủ gặp sự cố" giữa lúc khách đang đứng chờ thanh toán.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class InvoiceNumberingTests(SalonSysFactory factory)
{
    private const int Counters = 8;

    [Fact]
    public async Task Nhieu_quay_lap_hoa_don_cung_luc_deu_thanh_cong_va_so_khong_trung()
    {
        // Mỗi quầy một phiên riêng, như tám máy thật — không để tám request tranh nhau chạm
        // cùng một dòng phiên.
        var clients = await Task.WhenAll(Enumerable.Range(0, Counters)
            .Select(_ => SalonSysClient.TenantAdminAsync(factory, Lumiere)));

        try
        {
            var customerId = await ActiveCustomerIdAsync(clients[0]);

            var responses = await Task.WhenAll(clients.Select(client => client.PostAsync("/api/sales-invoices", new
            {
                customerId,
                branchId = BranchQ3,
                lines = new[] { new { name = "Lập đồng thời", unitPrice = 100_000L, quantity = 1 } }
            })));

            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.Status));

            var numbers = responses
                .Select(response => Text(response.Body.GetProperty("invoice"), "code")!)
                .Select(code => int.Parse(code[(code.LastIndexOf('-') + 1)..]))
                .Order()
                .ToArray();

            // Không trùng, và liền nhau: tám hóa đơn lấy đúng tám số kế tiếp, không nhảy cóc.
            Assert.Equal(Counters, numbers.Distinct().Count());
            Assert.Equal(numbers[0] + Counters - 1, numbers[^1]);
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }
}
