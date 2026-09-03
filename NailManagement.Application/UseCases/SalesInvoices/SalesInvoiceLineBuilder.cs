using NailManagement.Application.DTOs;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.SalesInvoices;

/// <summary>Một dòng hóa đơn đã được máy chủ chốt tên và giá, sẵn sàng ghi xuống.</summary>
public sealed record ResolvedInvoiceLine(string? ServiceId, string Name, long UnitPrice, int Quantity);

/// <summary>
/// Dựng các dòng hóa đơn từ thứ client gửi lên, hoặc từ các dịch vụ của một lịch hẹn.
/// <para>
/// Tách thành lớp dùng chung vì cả ba đường vào đều cần nó: lập hóa đơn từ lịch hẹn
/// (BR-INV-010), lập hóa đơn bán lẻ (BR-INV-011), và sửa trọn một hóa đơn chưa thu đủ
/// (BR-INV-015). Nếu mỗi đường tự đọc bảng giá thì sớm muộn có đường quên chốt giá ở máy chủ.
/// </para>
/// <para>
/// <b>Nguyên tắc xuyên suốt: giá của một dịch vụ có trong danh mục luôn lấy từ máy chủ</b>
/// (BR-SVC-007), kể cả khi client gửi kèm một con số. Chỉ mục nhập tay mới nhận giá từ client,
/// và đó là điều BR-INV-012 cho phép một cách có chủ đích — dòng ấy không tham chiếu bảng nào
/// nên không có nguồn nào khác để đối chiếu.
/// </para>
/// </summary>
public sealed class SalesInvoiceLineBuilder(IServiceRepository services)
{
    /// <summary>
    /// Dựng từ danh sách người dùng gửi lên. Mỗi dòng đi một trong hai nhánh: gắn dịch vụ, hoặc
    /// nhập tay.
    /// </summary>
    public async Task<IReadOnlyList<ResolvedInvoiceLine>> FromInputAsync(
        IReadOnlyList<SalesInvoiceLineInput>? inputs, CancellationToken cancellationToken = default)
    {
        if (inputs is null || inputs.Count == 0)
            throw DomainException.ForField("lines", "Hóa đơn phải có ít nhất một dòng.");

        var catalogue = await services.ListAsync(cancellationToken);
        var byId = catalogue.ToDictionary(service => service.Id);

        var resolved = new List<ResolvedInvoiceLine>(inputs.Count);

        foreach (var input in inputs)
        {
            var quantity = input.Quantity <= 0 ? 1 : input.Quantity;

            if (string.IsNullOrWhiteSpace(input.ServiceId))
            {
                // BR-INV-012 — mục nhập tay tự do. Bắt buộc có tên, nếu không thì hóa đơn in ra
                // sẽ có một dòng tiền không nói được nó là tiền gì.
                if (string.IsNullOrWhiteSpace(input.Name))
                {
                    throw DomainException.ForField(
                        "lines", "Dòng nhập tay phải có tên. Chọn một dịch vụ, hoặc điền tên cho dòng này.");
                }

                resolved.Add(new ResolvedInvoiceLine(
                    null, input.Name, Guard.Money(input.UnitPrice, "lines", "Đơn giá"), quantity));

                continue;
            }

            if (!byId.TryGetValue(input.ServiceId, out var service))
            {
                throw DomainException.ForField(
                    "lines", $"Không tìm thấy dịch vụ {input.ServiceId} trong bảng giá của tiệm.");
            }

            // Dịch vụ đã ngừng bán vẫn lập hóa đơn được, khác hẳn lúc đặt lịch. Buổi làm đã
            // diễn ra thật; chặn ở đây là để khách được phục vụ rồi mà quầy không thu tiền được.
            resolved.Add(new ResolvedInvoiceLine(service.Id, service.Name, service.Price, quantity));
        }

        return resolved;
    }

    /// <summary>
    /// BR-INV-010 — dựng từ các dịch vụ của một lịch hẹn khi lễ tân bấm "Thanh toán".
    /// <para>
    /// Lấy <b>giá hiện tại</b> của dịch vụ chứ không phải giá lúc đặt lịch: BR-SVC-007 quy định
    /// lịch hẹn chưa hoàn tất thì tính theo bảng giá tại thời điểm lập hóa đơn, và đó cũng là lý
    /// do <c>AppointmentService</c> cố ý không chép giá vào dòng của nó.
    /// </para>
    /// <para>
    /// Tên cũng lấy theo bản ghi dịch vụ hiện tại, không lấy bản chép trên dòng lịch hẹn: hóa
    /// đơn được in ra hôm nay, nên nó phải gọi dịch vụ bằng đúng cái tên mà bảng giá treo ở tiệm
    /// đang dùng. Bản chép trên lịch hẹn phục vụ việc khác — đọc lại một lịch hẹn cũ đúng như
    /// lúc nó được đặt (BR-DEL-003).
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ResolvedInvoiceLine>> FromAppointmentAsync(
        Appointment appointment, CancellationToken cancellationToken = default)
    {
        var catalogue = await services.ListAsync(cancellationToken);
        var byId = catalogue.ToDictionary(service => service.Id);

        var resolved = new List<ResolvedInvoiceLine>(appointment.Services.Count);

        foreach (var line in appointment.Services)
        {
            // Dịch vụ bị gỡ hẳn khỏi bảng giá là chuyện BR-DEL-001 không cho xảy ra, nhưng nếu
            // có thì vẫn phải thu được tiền: rơi về tên đã chép trên lịch hẹn, giá 0, để người ở
            // quầy sửa lại thành mục nhập tay thay vì gặp một lỗi không hiểu vì sao.
            resolved.Add(byId.TryGetValue(line.ServiceId, out var service)
                ? new ResolvedInvoiceLine(service.Id, service.Name, service.Price, 1)
                : new ResolvedInvoiceLine(null, line.ServiceName, 0, 1));
        }

        return resolved;
    }
}
