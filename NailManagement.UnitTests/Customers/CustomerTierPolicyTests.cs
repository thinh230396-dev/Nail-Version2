using NailManagement.Domain.Salon.Customers;
using NailManagement.Domain.Shared;

namespace NailManagement.UnitTests.Customers;

/// <summary>Hạng khách theo tổng chi — BR-CUS-007. Kiểm đúng hai mép của mỗi ngưỡng.</summary>
public sealed class CustomerTierPolicyTests
{
    private const long Loyal = ValidationPolicy.CustomerLoyalThreshold;
    private const long Vip = ValidationPolicy.CustomerVipThreshold;

    [Theory]
    [InlineData(0, 0L, CustomerTier.New)]
    [InlineData(0, Vip, CustomerTier.New)]          // chưa có hóa đơn nào thì vẫn là khách mới
    [InlineData(1, 0L, CustomerTier.Standard)]
    [InlineData(3, Loyal - 1, CustomerTier.Standard)]
    [InlineData(3, Loyal, CustomerTier.Loyal)]
    [InlineData(9, Vip - 1, CustomerTier.Loyal)]
    [InlineData(9, Vip, CustomerTier.Vip)]
    public void Hang_khach_theo_so_lan_ghe_va_tong_chi(int invoices, long spent, CustomerTier expected)
        => Assert.Equal(expected, CustomerTierPolicy.Resolve(invoices, spent));
}
