using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.DTOs.Salon;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Policies;
using NailManagement.Domain.Repositories.Salon;

namespace NailManagement.Application.UseCases.Reports;

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
public sealed class GetRevenueReportUseCase(IRevenueRepository revenue, IClock clock)
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
            .Where(slice => slice.Collected != 0 || slice.Revenue != 0)
            .ToList();

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
            ByBranch(slices),
            ByStaff(slices),
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
        DateTimeOffset FirstPaidAt,
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
            rows.Count > 0 ? rows[0].PaidAt : invoice.CreatedAt,
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
            .GroupBy(slice => DateOnly.FromDateTime(slice.FirstPaidAt.ToOffset(SalonTime.Offset).Date))
            .OrderBy(group => group.Key)
            .Select(group => new RevenueBreakdownRow(
                group.Key.ToString("yyyy-MM-dd"),
                group.Key.ToString("dd/MM/yyyy"),
                group.Sum(slice => slice.Revenue),
                group.Sum(slice => slice.Collected),
                group.Count()))];

    private static IReadOnlyList<RevenueBreakdownRow> ByBranch(IEnumerable<InvoiceSlice> slices)
        => [.. slices
            .GroupBy(slice => slice.Invoice.BranchId)
            .Select(group => new RevenueBreakdownRow(
                group.Key,
                group.First().Invoice.Branch?.Name ?? group.Key,
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
    private static IReadOnlyList<StaffRevenueRow> ByStaff(IEnumerable<InvoiceSlice> slices)
        => [.. slices
            .GroupBy(slice => slice.Invoice.StaffId ?? string.Empty)
            .Select(group =>
            {
                var staff = group.First().Invoice.Staff;
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
    /// Gom theo <b>tên</b> chứ không theo mã dịch vụ: BR-INV-012 cho phép dòng nhập tay không
    /// gắn mã nào, và một báo cáo bỏ qua chúng sẽ thiếu đúng phần bán lẻ tại quầy.
    /// </para>
    /// </summary>
    private static IReadOnlyList<RevenueBreakdownRow> ByService(IEnumerable<InvoiceSlice> slices)
        => [.. slices
            .SelectMany(slice => slice.Invoice.Lines.Select(line => new
            {
                Key = line.ServiceId ?? line.Name,
                line.Name,
                Revenue = RevenuePolicy.LineShare(slice.Revenue, line.LineTotal, slice.Invoice.Subtotal),
                Collected = RevenuePolicy.LineShare(slice.Collected, line.LineTotal, slice.Invoice.Subtotal)
            }))
            .GroupBy(entry => entry.Key)
            .Select(group => new RevenueBreakdownRow(
                group.Key,
                group.First().Name,
                group.Sum(entry => entry.Revenue),
                group.Sum(entry => entry.Collected),
                group.Count()))
            .OrderByDescending(row => row.Revenue)];
}
