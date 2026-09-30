using System.Net;
using NailManagement.Tests.Infrastructure;

using static NailManagement.Tests.Scenarios.SalonScenario;

namespace NailManagement.Tests.Concurrency;

/// <summary>
/// BR-APT-011 — một kỹ thuật viên không có hai lịch chồng nhau, <b>kể cả khi hai quầy bấm đặt
/// cùng một lúc</b>.
/// <para>
/// <c>BookingConflictTests</c> kiểm luật chống trùng khi các lần đặt đi lần lượt. Ở đây chúng đi
/// song song: phép kiểm "khung giờ còn trống không" và lệnh ghi là hai bước, và giữa hai bước ấy
/// một quầy khác có thể đã chen vào. Không có chỉ mục duy nhất nào bắt được hai khoảng thời gian
/// <i>chồng lên nhau</i>, nên nếu hàng rào ở tầng ứng dụng hở thì lịch trùng được lưu im lặng.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class DoubleBookingTests(SalonSysFactory factory)
{
    private const int Counters = 6;

    [Fact]
    public async Task Nhieu_quay_dat_cung_mot_khung_gio_chi_mot_quay_thanh_cong()
    {
        var clients = await Task.WhenAll(Enumerable.Range(0, Counters)
            .Select(_ => SalonSysClient.TenantAdminAsync(factory, Lumiere)));

        try
        {
            var customerId = await ActiveCustomerIdAsync(clients[0]);
            var staffId = await TechnicianIdAsync(clients[0], BranchQ3);
            var serviceId = Text(await ActiveServiceAsync(clients[0]), "id")!;
            var slot = NextSlot();

            var responses = await Task.WhenAll(clients.Select(client =>
                TryBookAsync(client, customerId, staffId, slot, [serviceId])));

            Assert.Single(responses, response => response.Status == HttpStatusCode.Created);
            Assert.All(
                responses.Where(response => response.Status != HttpStatusCode.Created),
                response =>
                {
                    Assert.Equal(HttpStatusCode.Conflict, response.Status);
                    Assert.Equal("SLOT_CONFLICT", response.ErrorCode);
                });
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }
}
