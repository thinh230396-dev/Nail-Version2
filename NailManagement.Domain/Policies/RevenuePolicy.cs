using System.Numerics;

namespace NailManagement.Domain.Policies;

/// <summary>
/// Công thức doanh thu ở BR-REV-001, viết thành hàm thuần trên kiểu <c>long</c> VND.
///
/// <para>
/// BR-REV-001 nói doanh thu ghi nhận theo <b>tiền thực thu</b> (cơ sở tiền mặt), không theo
/// lịch hẹn hoàn tất:
/// </para>
/// <code>
/// doanh_thu = Σ(invoice_payments.amount) − Σ(tip của các hóa đơn liên quan)
/// </code>
/// <para>
/// Dòng hoàn tiền mang số âm (BR-PAY-006) nên phép cộng dồn đã tự trừ nó — và vì mỗi dòng có
/// mốc thời gian riêng, hoàn tiền tự động làm giảm doanh thu <b>tại ngày hoàn</b> chứ không
/// sửa lại ngày cũ, đúng BR-REV-003.
/// </para>
/// </summary>
public static class RevenuePolicy
{
    /// <summary>
    /// Phần của một đồng tiền thu được **thuộc về doanh thu tiệm**, sau khi loại tip.
    ///
    /// <para>
    /// BR-REV-002 — tip nằm trong số tiền khách trả nhưng không thuộc doanh thu. Câu chữ của
    /// BR-REV-001 là "trừ đi tip của các hóa đơn liên quan", và cách trừ **theo tỉ lệ** ở đây
    /// cho ra đúng con số ấy khi hóa đơn đã thu đủ: thu trọn <c>Total</c> rồi nhân với
    /// <c>(Total − Tip) / Total</c> thì còn lại đúng <c>Total − Tip</c>.
    /// </para>
    /// <para>
    /// Vì sao theo tỉ lệ thay vì trừ nguyên cục tip một lần: hóa đơn thu làm nhiều lần
    /// (BR-PAY-004) có nhiều mốc thu, có thể rơi vào nhiều ngày khác nhau. Trừ nguyên cục thì
    /// phải chọn một ngày để gánh toàn bộ tip, và ngày đó có thể ra doanh thu <b>âm</b> trong
    /// khi tiệm vẫn thu được tiền. Trừ theo tỉ lệ thì mỗi ngày gánh đúng phần của mình.
    /// </para>
    /// </summary>
    /// <param name="invoiceTotal">Tổng phải trả của hóa đơn — đã gồm tip (BR-INV-020).</param>
    /// <param name="invoiceTip">Phần tip trong tổng đó.</param>
    public static decimal NetShare(long invoiceTotal, long invoiceTip)
    {
        if (invoiceTotal <= 0) return 1m;

        var net = invoiceTotal - invoiceTip;

        // Tip lớn hơn cả tổng là dữ liệu hỏng, không phải một tình huống nghiệp vụ. Kẹp về 0
        // thay vì trả số âm: một hóa đơn không bao giờ làm doanh thu giảm chỉ vì có tip.
        if (net <= 0) return 0m;

        return (decimal)net / invoiceTotal;
    }

    /// <summary>
    /// Doanh thu tiệm từ một số tiền đã thu trên một hóa đơn cụ thể.
    ///
    /// <para>
    /// Làm tròn về số nguyên VND theo quy tắc ngân hàng (BR-VAL-003 — tiền tệ là số nguyên,
    /// không có phần thập phân nào để giữ). Sai số làm tròn tối đa là một đồng cho mỗi dòng
    /// thu, và nó không tích lũy vì mỗi dòng được tính độc lập từ số gốc.
    /// </para>
    /// </summary>
    public static long NetRevenue(long collectedAmount, long invoiceTotal, long invoiceTip)
        => (long)Math.Round(collectedAmount * NetShare(invoiceTotal, invoiceTip), MidpointRounding.ToEven);

    /// <summary>
    /// Phần doanh thu quy về **một dòng hàng** của hóa đơn — chiều "dịch vụ" của BR-REV-004.
    ///
    /// <para>
    /// Ba chiều còn lại (ngày, chi nhánh, nhân viên) gắn thẳng được vào dòng thu tiền, nhưng
    /// dịch vụ thì nằm ở dòng hàng trong khi tiền vào theo cả hóa đơn. Một hóa đơn hai dịch vụ
    /// mới thu một nửa thì mỗi dịch vụ đã mang về bao nhiêu — không rule nào trả lời, và đây là
    /// quyết định 59 chốt ngày 16: <b>phân bổ theo tỉ lệ giá trị dòng</b>.
    /// </para>
    /// <para>
    /// Hệ quả quan trọng: vì tổng các tỉ lệ bằng 1, <b>cộng cả bốn chiều lại luôn ra cùng một
    /// con số tổng</b>. Không ai phải giải thích vì sao bảng này khác bảng kia — đó chính là
    /// câu hỏi mà một cách tính khác sẽ tạo ra.
    /// </para>
    /// </summary>
    /// <param name="invoiceSubtotal">
    /// Tổng tiền hàng trước giảm giá. Dùng làm mẫu số để giảm giá cũng được chia đều theo tỉ
    /// lệ cho từng dòng, thay vì trút hết vào một dòng nào đó.
    /// </param>
    public static long LineShare(long netRevenue, long lineTotal, long invoiceSubtotal)
    {
        if (invoiceSubtotal <= 0 || lineTotal <= 0) return 0L;

        return (long)Math.Round(netRevenue * ((decimal)lineTotal / invoiceSubtotal), MidpointRounding.ToEven);
    }

    /// <summary>Phân bổ theo giá trị dòng, chia phần dư để giữ nguyên tổng tới từng đồng.</summary>
    public static IReadOnlyList<long> AllocateToLines(long amount, IReadOnlyList<long> lineTotals)
    {
        if (lineTotals.Any(total => total < 0))
            throw new ArgumentOutOfRangeException(nameof(lineTotals), "Giá trị dòng không được âm.");

        var weight = lineTotals.Aggregate(BigInteger.Zero, (sum, total) => sum + total);
        if (weight == 0 || amount == 0) return new long[lineTotals.Count];

        var magnitude = BigInteger.Abs(new BigInteger(amount));
        var shares = new BigInteger[lineTotals.Count];
        var remainders = new BigInteger[lineTotals.Count];
        for (var index = 0; index < lineTotals.Count; index++)
            shares[index] = BigInteger.DivRem(magnitude * lineTotals[index], weight, out remainders[index]);

        var remaining = (int)(magnitude - shares.Aggregate(BigInteger.Zero, (sum, share) => sum + share));
        foreach (var index in Enumerable.Range(0, shares.Length)
                     .OrderByDescending(index => remainders[index]).ThenBy(index => index).Take(remaining))
            shares[index]++;

        return shares.Select(share => (long)(amount < 0 ? -share : share)).ToArray();
    }

    /// <summary>
    /// BR-EMP-011 — tiền hoa hồng là **một phép nhân lúc hiển thị**, không có bảng nào lưu nó.
    ///
    /// <para>
    /// BR-REV-005 chỉ rõ nguồn: doanh thu theo nhân viên. Không có kỳ chốt, không có duyệt chi,
    /// nên con số này luôn tính lại từ dữ liệu hiện tại mỗi lần mở báo cáo — đổi tỉ lệ hoa hồng
    /// hôm nay sẽ đổi luôn con số của tháng trước, và đó là hành vi đã biết của một MVP không
    /// có bảng lương.
    /// </para>
    /// </summary>
    public static long Commission(long staffRevenue, decimal commissionRate)
        => staffRevenue <= 0 || commissionRate <= 0m
            ? 0L
            : (long)Math.Round(staffRevenue * commissionRate, MidpointRounding.ToEven);
}
