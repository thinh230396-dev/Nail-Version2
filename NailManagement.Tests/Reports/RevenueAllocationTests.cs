using NailManagement.Domain.Salon.Revenue;

namespace NailManagement.Tests.Reports;

public sealed class RevenueAllocationTests
{
    [Theory]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(1_000_001L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void Allocation_preserves_signed_total_including_long_boundaries(long amount)
    {
        var shares = RevenuePolicy.AllocateToLines(amount, [1, 2, 7, 0]);

        Assert.Equal(amount, shares.Sum());
        Assert.Equal(0, shares[3]);
        Assert.All(shares, share => Assert.True(amount < 0 ? share <= 0 : share >= 0));
    }

    [Fact]
    public void Exact_proportions_are_preserved()
        => Assert.Equal(new long[] { 10, 20, 70 }, RevenuePolicy.AllocateToLines(100, [1, 2, 7]));

    [Fact]
    public void Allocation_of_refund_is_the_inverse_of_payment_allocation()
    {
        var payment = RevenuePolicy.AllocateToLines(101, [1, 1, 1]);
        var refund = RevenuePolicy.AllocateToLines(-101, [1, 1, 1]);

        Assert.Equal(payment.Select(share => -share), refund);
    }
}
