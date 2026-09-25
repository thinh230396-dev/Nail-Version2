using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Features.SalesInvoices;
using NailManagement.Domain.Salon.Invoices;
using NailManagement.Domain.Shared;

namespace NailManagement.Application.Features.SalesInvoices.UseCases;

/// <summary>
/// Sổ hóa đơn bán hàng của tiệm đang làm việc trong một khoảng ngày.
/// <para>
/// Cùng khuôn với bảng lịch hẹn ở ngày 11 — khoảng ngày, trần 92 ngày, chi nhánh lấy từ phiên
/// đăng nhập — vì hai màn hình này có cùng một hình dạng vấn đề: dữ liệu cộng dồn mãi theo thời
/// gian, còn người dùng thì luôn xem một ngày hoặc một tháng.
/// </para>
/// <para>
/// BR-ISO-004 xếp hóa đơn bán hàng cùng nhóm với lịch hẹn và nhân viên: lễ tân chỉ thấy chi
/// nhánh mình. Khác nhóm khách hàng và dịch vụ, thứ mà lễ tân thấy toàn tiệm.
/// </para>
/// </summary>
public sealed class ListSalesInvoicesUseCase(ISalesInvoiceRepository invoices, IClock clock)
{
    /// <summary>
    /// Trần độ dài khoảng ngày, giống bảng lịch hẹn. Không có nó thì một chuỗi truy vấn đủ rộng
    /// sẽ kéo về toàn bộ sổ hóa đơn của tiệm.
    /// </summary>
    private const int MaxRangeDays = 92;

    /// <param name="from">
    /// Rỗng thì lấy 0 giờ hôm nay theo giờ tiệm (<see cref="SalonTime"/>). Hóa đơn được chọn
    /// theo <b>giờ lập</b>, tức thời điểm quầy bấm thanh toán — không theo giờ thu tiền, vì một
    /// hóa đơn có thể được thu làm nhiều lần vào nhiều ngày khác nhau (BR-PAY-004).
    /// </param>
    public async Task<IReadOnlyList<SalesInvoiceDto>> ExecuteAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var start = from ?? SalonTime.StartOfToday(clock.UtcNow);
        var end = to ?? start.AddDays(1);

        if (end < start)
            throw DomainException.ForField("to", "Ngày kết thúc phải sau ngày bắt đầu.");

        if ((end - start).TotalDays > MaxRangeDays)
        {
            throw DomainException.ForField(
                "to", $"Khoảng ngày không được dài quá {MaxRangeDays} ngày. Hãy xem theo từng tháng.");
        }

        var branchId = BranchScope.Resolve(actor, "sổ hóa đơn");
        var found = await invoices.ListAsync(start, end, branchId, cancellationToken);

        return [.. found.Select(SalesInvoiceMapper.ToDto)];
    }
}
