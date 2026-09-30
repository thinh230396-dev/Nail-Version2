using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Features.Reports;
using NailManagement.Domain.Salon.Invoices;
using NailManagement.Domain.Salon.Revenue;
using NailManagement.Domain.Shared;

namespace NailManagement.Application.Features.Reports.UseCases;

/// <summary>
/// Báo cáo doanh thu bốn chiều — BR-REV-001…005, và là bước cuối của mạch demo.
///
/// <para>
/// Toàn bộ phép tính tiền nằm ở <c>RevenuePolicy</c> tầng Domain; use case này chỉ lấy dữ
/// liệu, gom nhóm, và dựng DTO. Đó là chủ đích: công thức doanh thu là một trong bốn hạng mục
/// mà §6 của lộ trình đánh dấu <b>tuyệt đối không cắt</b>, nên nó phải nằm ở chỗ viết được test
/// mà không cần database hay HTTP.
/// </para>
/// <para>
/// <b>Chỉ chủ tiệm gọi được.</b> Ma trận mục 3.4 để trống ô <c>RevenueReports</c> cho cả
/// Superadmin (BR-AUTH-030 — họ không chạm dữ liệu nghiệp vụ trong tiệm) lẫn lễ tân (doanh thu
/// tiệm không phải việc của quầy). Vì vậy use case này <b>không</b> gọi <c>BranchScope</c>: nó
/// không có người dùng nào bị thu hẹp theo chi nhánh, và mã chi nhánh ở đây là bộ lọc người
/// dùng tự chọn trên màn hình.
/// </para>
/// </summary>
public sealed class GetRevenueReportUseCase(
    IRevenueRepository revenue,
    ISalonDirectoryReader directory,
    IClock clock)
{
    /// <summary>
    /// Trần độ dài khoảng ngày, giống hai endpoint đọc theo khoảng đã có. Chọn 366 chứ không
    /// phải 92 như sổ hóa đơn: báo cáo doanh thu là chỗ người ta thật sự muốn xem cả năm, và
    /// phép gom nhóm ở đây trả về vài chục dòng chứ không trả về từng bản ghi.
    /// </summary>
    private const int MaxRangeDays = 366;

    public async Task<RevenueReportDto> ExecuteAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? branchId,
        CancellationToken cancellationToken = default)
    {
        var start = from ?? SalonTime.StartOfToday(clock.UtcNow);
        var end = to ?? start.AddDays(1);

        if (end < start)
            throw DomainException.ForField("to", "Ngày kết thúc phải sau ngày bắt đầu.");

        if ((end - start).TotalDays > MaxRangeDays)
        {
            throw DomainException.ForField(
                "to", $"Khoảng ngày không được dài quá {MaxRangeDays} ngày.");
        }

        var scope = string.IsNullOrWhiteSpace(branchId) ? null : branchId.Trim();
        var invoices = await revenue.ListCollectedBetweenAsync(start, end, scope, cancellationToken);

        var slices = invoices
            .Select(invoice => Slice(invoice, start, end))
            .Where(slice => slice.Payments.Count > 0)
            .ToList();

        var names = await directory.LoadAsync(
            slices.Select(slice => slice.Invoice.BranchId),
            [],
            slices.Select(slice => slice.Invoice.StaffId),
            cancellationToken);

        return new RevenueReportDto(
            start,
            end,
            scope,
            slices.Sum(slice => slice.Revenue),
            slices.Sum(slice => slice.Collected),
            slices.Sum(slice => slice.Tip),
            slices.Sum(slice => slice.Refunded),
            slices.Count,
            ByDay(slices),
            ByBranch(slices, names),
            ByStaff(slices, names),
            ByService(slices));
    }

    /// <summary>
    /// Phần của một hóa đơn thuộc về khoảng thời gian đang xem.
    ///
    /// <para>
    /// Một hóa đơn thu làm nhiều lần có thể có dòng thu nằm trong khoảng và dòng thu nằm ngoài;
    /// chỉ những dòng bên trong mới được tính. Đây chính là chỗ BR-REV-003 thành hiện thực —
    /// một lần hoàn tiền tháng này làm giảm doanh thu tháng này, còn báo cáo tháng trước vẫn
    /// giữ nguyên con số cũ.
    /// </para>
    /// </summary>
    private sealed record InvoiceSlice(
        SalesInvoice Invoice,
        IReadOnlyList<InvoicePayment> Payments,
        long Collected,
        long Revenue,
        long Tip,
        long Refunded);

    private static InvoiceSlice Slice(SalesInvoice invoice, DateTimeOffset from, DateTimeOffset to)
    {
        var rows = invoice.Payments
            .Where(payment => payment.PaidAt >= from && payment.PaidAt < to)
            .OrderBy(payment => payment.PaidAt)
            .ToList();

        var collected = rows.Sum(payment => payment.Amount);
        var revenue = rows.Sum(payment => RevenuePolicy.NetRevenue(payment.Amount, invoice.Total, invoice.Tip));

        return new InvoiceSlice(
            invoice,
            rows,
            collected,
            revenue,

            // Chênh lệch giữa tiền vào két và doanh thu chính là tip — không cần cộng lại từ
            // cột `tip` của hóa đơn, và không nên: cột ấy là tip của TOÀN hóa đơn, còn ở đây
            // ta chỉ đang nói về phần đã thu trong khoảng này.
            collected - revenue,
            rows.Where(payment => payment.Type == PaymentType.Refund).Sum(payment => -payment.Amount));
    }

    /// <summary>
    /// Chiều "ngày" — theo <b>giờ tiệm</b>, không theo UTC.
    ///
    /// <para>
    /// Dùng UTC ở đây là lỗi im lặng và chỉ sai trong bảy tiếng mỗi ngày: mọi khoản thu từ 0
    /// giờ tới 7 giờ sáng giờ Việt Nam sẽ bị xếp vào ngày hôm trước. Chủ tiệm mở báo cáo sẽ
    /// thấy doanh thu ca sáng rơi sang hôm qua mà không hiểu vì sao.
    /// </para>
    /// </summary>
    private static IReadOnlyList<RevenueBreakdownRow> ByDay(IEnumerable<InvoiceSlice> slices)
        => [.. slices
            .SelectMany(slice => slice.Payments.Select(payment => new
            {
                slice.Invoice.Id,
                Day = DateOnly.FromDateTime(payment.PaidAt.ToOffset(SalonTime.Offset).Date),
                Collected = payment.Amount,
                Revenue = RevenuePolicy.NetRevenue(payment.Amount, slice.Invoice.Total, slice.Invoice.Tip)
            }))
            .GroupBy(payment => payment.Day)
            .OrderBy(group => group.Key)
            .Select(group => new RevenueBreakdownRow(
                group.Key.ToString("yyyy-MM-dd"),
                group.Key.ToString("dd/MM/yyyy"),
                group.Sum(slice => slice.Revenue),
                group.Sum(slice => slice.Collected),
                group.Select(payment => payment.Id).Distinct().Count()))];

    private static IReadOnlyList<RevenueBreakdownRow> ByBranch(IEnumerable<InvoiceSlice> slices, SalonDirectory names)
        => [.. slices
            .GroupBy(slice => slice.Invoice.BranchId)
            .Select(group => new RevenueBreakdownRow(
                group.Key,
                names.BranchOrNull(group.Key)?.Name ?? group.Key,
                group.Sum(slice => slice.Revenue),
                group.Sum(slice => slice.Collected),
                group.Count()))
            .OrderByDescending(row => row.Revenue)];

    /// <summary>
    /// Chiều "nhân viên" — BR-REV-005, và là nguồn duy nhất của tiền hoa hồng ở BR-EMP-011.
    ///
    /// <para>
    /// Hóa đơn không ghi công cho ai vẫn được gom thành một dòng riêng thay vì bị bỏ đi: nếu
    /// bỏ, tổng của bảng này sẽ thấp hơn ba bảng kia và không ai giải thích được vì sao. Dòng
    /// ấy mang khóa rỗng và tỉ lệ hoa hồng bằng 0.
    /// </para>
    /// </summary>
    private static IReadOnlyList<StaffRevenueRow> ByStaff(IEnumerable<InvoiceSlice> slices, SalonDirectory names)
        => [.. slices
            .GroupBy(slice => slice.Invoice.StaffId ?? string.Empty)
            .Select(group =>
            {
                var staff = names.StaffMemberOrNull(group.First().Invoice.StaffId);
                var earned = group.Sum(slice => slice.Revenue);
                var rate = staff?.CommissionRate ?? 0m;

                return new StaffRevenueRow(
                    group.Key,
                    staff?.FullName ?? "Chưa ghi công",
                    earned,
                    group.Sum(slice => slice.Collected),
                    group.Count(),
                    rate,
                    RevenuePolicy.Commission(earned, rate));
            })
            .OrderByDescending(row => row.Revenue)];

    /// <summary>
    /// Chiều "dịch vụ" — quyết định 59 ngày 16: phân bổ doanh thu của hóa đơn cho từng dòng
    /// hàng <b>theo tỉ lệ giá trị dòng</b>.
    ///
    /// <para>
    /// Ba chiều trên gắn thẳng được vào dòng thu tiền; chiều này thì không, vì dịch vụ nằm ở
    /// dòng hàng còn tiền vào theo cả hóa đơn. Nhờ tổng các tỉ lệ bằng 1, bảng này vẫn cộng
    /// lại ra đúng con số tổng như ba bảng kia.
    /// </para>
    /// <para>
    /// Gom theo mã dịch vụ khi có mã; dòng nhập tay dùng tên làm khóa (BR-INV-012).
    /// Chia phần dư giữa các dòng để tổng phân bổ giữ nguyên đến từng đồng.
    /// </para>
    /// </summary>
    private static IReadOnlyList<RevenueBreakdownRow> ByService(IEnumerable<InvoiceSlice> slices)
        => [.. slices
            .SelectMany(AllocateServices)
            .GroupBy(entry => entry.Key)
            .Select(group => new RevenueBreakdownRow(
                group.Key,
                group.First().Name,
                group.Sum(entry => entry.Revenue),
                group.Sum(entry => entry.Collected),
                group.Select(entry => entry.InvoiceId).Distinct().Count()))
            .OrderByDescending(row => row.Revenue)];

    private sealed record ServiceSlice(string Key, string Name, string InvoiceId, long Revenue, long Collected);

    private static IEnumerable<ServiceSlice> AllocateServices(InvoiceSlice slice)
    {
        // Cố định thứ tự để phép chia phần dư không thay đổi theo thứ tự EF nạp dòng hàng.
        var lines = slice.Invoice.Lines.OrderBy(line => line.Id, StringComparer.Ordinal).ToArray();
        if (slice.Invoice.Subtotal == 0 || lines.Length == 0)
        {
            yield return new ServiceSlice("__unallocated__", "Chưa phân bổ",
                slice.Invoice.Id, slice.Revenue, slice.Collected);
            yield break;
        }

        var weights = lines.Select(line => line.LineTotal).ToArray();
        var revenue = RevenuePolicy.AllocateToLines(slice.Revenue, weights);
        var collected = RevenuePolicy.AllocateToLines(slice.Collected, weights);
        for (var index = 0; index < lines.Length; index++)
            yield return new ServiceSlice(lines[index].ServiceId ?? lines[index].Name, lines[index].Name,
                slice.Invoice.Id, revenue[index], collected[index]);
    }
}
