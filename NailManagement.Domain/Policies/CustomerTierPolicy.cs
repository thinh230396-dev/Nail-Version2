using NailManagement.Domain.Enums;

namespace NailManagement.Domain.Policies;

/// <summary>
/// BR-CUS-007 — hạng khách suy ra từ tổng chi tiêu.
/// <para>
/// Là một hàm thuần, không phải một cột: BR-CUS-009 quy định tổng chi tiêu được tính từ
/// hóa đơn đã thanh toán chứ không lưu sẵn. Lưu cột đếm thì mỗi lần hoàn tiền lại phải
/// nhớ cập nhật ngược, và chỉ cần quên một chỗ là hạng khách sai vĩnh viễn.
/// </para>
/// </summary>
public static class CustomerTierPolicy
{
    /// <param name="invoiceCount">Số hóa đơn đã thanh toán. Chưa có hóa đơn nào thì hạng là New.</param>
    /// <param name="totalSpent">Tổng tiền khách đã trả, VND.</param>
    public static CustomerTier Resolve(int invoiceCount, long totalSpent)
    {
        if (invoiceCount <= 0) return CustomerTier.New;
        if (totalSpent >= ValidationPolicy.CustomerVipThreshold) return CustomerTier.Vip;
        if (totalSpent >= ValidationPolicy.CustomerLoyalThreshold) return CustomerTier.Loyal;

        return CustomerTier.Standard;
    }
}
