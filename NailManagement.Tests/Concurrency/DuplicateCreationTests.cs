using System.Net;
using NailManagement.Tests.Infrastructure;

using static NailManagement.Tests.Scenarios.SalonScenario;

namespace NailManagement.Tests.Concurrency;

/// <summary>
/// Hai quầy tạo cùng một thứ phải là duy nhất, cùng một lúc.
/// <para>
/// Use case kiểm "đã có chưa" rồi mới ghi. Đi lần lượt thì phép kiểm trả 422 kèm câu chữ gắn đúng
/// ô nhập. Đi song song thì cả hai cùng qua phép kiểm, và chỉ mục duy nhất ở database mới là thứ
/// chặn bản thứ hai — dữ liệu vẫn đúng, nhưng quầy thứ hai từng nhận HTTP 500 "máy chủ gặp sự cố"
/// thay vì câu "tiệm đã có khách mang số này" mà nó sẽ nhận nếu bấm chậm hơn một chút.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class DuplicateCreationTests(SalonSysFactory factory)
{
    private const int Counters = 6;

    [Fact]
    public async Task Hai_quay_cung_tao_mot_so_dien_thoai_thi_quay_sau_nhan_loi_422_dung_o()
    {
        var clients = await Task.WhenAll(Enumerable.Range(0, Counters)
            .Select(_ => SalonSysClient.TenantAdminAsync(factory, Lumiere)));

        try
        {
            // Số chưa ai dùng ở mỗi lần chạy, để phép thử không đụng dữ liệu mẫu hay lần chạy trước.
            var phone = $"09{Random.Shared.Next(10_000_000, 99_999_999)}";

            var responses = await Task.WhenAll(clients.Select(client => client.PostAsync(
                "/api/customers", new { phone, fullName = "Khách tạo đồng thời" })));

            Assert.Single(responses, response => response.Status == HttpStatusCode.Created);
            Assert.All(
                responses.Where(response => response.Status != HttpStatusCode.Created),
                response =>
                {
                    Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
                    Assert.Equal("VALIDATION_FAILED", response.ErrorCode);
                    Assert.Contains("phone", FieldNames(response));
                });
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }
}
