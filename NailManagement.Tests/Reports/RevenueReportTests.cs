using NailManagement.Application.Abstractions;
using NailManagement.Application.Features.Reports;
using NailManagement.Application.Features.Reports.UseCases;
using NailManagement.Domain.Salon.Invoices;
using NailManagement.Domain.Salon.Revenue;

namespace NailManagement.Tests.Reports;

public sealed class RevenueReportTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public async Task Installments_are_reported_on_their_actual_payment_days()
    {
        var invoice = Invoice(600_000);
        Pay(invoice, 200_000, Start.AddHours(9));
        Pay(invoice, 400_000, Start.AddDays(1).AddHours(9));

        var report = await Report(invoice);

        Assert.Equal(600_000, report.Collected);
        Assert.Collection(report.ByDay,
            row => { Assert.Equal("2026-09-01", row.Key); Assert.Equal(200_000, row.Collected); },
            row => { Assert.Equal("2026-09-02", row.Key); Assert.Equal(400_000, row.Collected); });
    }

    [Fact]
    public async Task Multiple_payments_on_one_day_count_the_invoice_once()
    {
        var invoice = Invoice(600_000);
        Pay(invoice, 200_000, Start.AddHours(9));
        Pay(invoice, 400_000, Start.AddHours(10));

        var row = Assert.Single((await Report(invoice)).ByDay);

        Assert.Equal(600_000, row.Collected);
        Assert.Equal(1, row.InvoiceCount);
    }

    [Fact]
    public async Task Full_refund_keeps_both_days_even_when_the_period_total_is_zero()
    {
        var invoice = Invoice(600_000);
        Pay(invoice, 600_000, Start.AddHours(9));
        invoice.IssueRefund("PAY-REFUND", PaymentMethod.Cash, 600_000,
            Start.AddDays(1).AddHours(9), "Hoàn tiền kiểm thử", "USR-TEST", Start.AddDays(1));

        var report = await Report(invoice);

        Assert.Equal(0, report.Collected);
        Assert.Equal(600_000, report.Refunds);
        Assert.Equal(1, report.InvoiceCount);
        Assert.Collection(report.ByDay,
            row => Assert.Equal(600_000, row.Collected),
            row => Assert.Equal(-600_000, row.Collected));
    }

    [Fact]
    public async Task Report_only_includes_payments_inside_the_requested_period()
    {
        var invoice = Invoice(600_000);
        Pay(invoice, 200_000, Start.AddHours(9));
        Pay(invoice, 400_000, Start.AddDays(1).AddHours(9));

        var report = await Report(invoice, Start.AddDays(1), Start.AddDays(2));

        Assert.Equal(400_000, report.Collected);
        Assert.Equal("2026-09-02", Assert.Single(report.ByDay).Key);
    }

    [Fact]
    public async Task Payment_day_uses_salon_timezone_instead_of_utc()
    {
        var invoice = Invoice(100_000);
        Pay(invoice, 100_000, new DateTimeOffset(2026, 9, 1, 20, 0, 0, TimeSpan.Zero));

        var report = await Report(invoice);

        Assert.Equal("2026-09-02", Assert.Single(report.ByDay).Key);
    }

    [Fact]
    public async Task Tips_are_excluded_from_each_days_revenue()
    {
        var invoice = Invoice(600_000);
        invoice.SetTip(60_000, Start);
        Pay(invoice, 220_000, Start.AddHours(9));
        Pay(invoice, 440_000, Start.AddDays(1).AddHours(9));

        var report = await Report(invoice);

        Assert.Equal(60_000, report.Tips);
        Assert.Equal(600_000, report.Revenue);
        Assert.Collection(report.ByDay,
            row => Assert.Equal(200_000, row.Revenue),
            row => Assert.Equal(400_000, row.Revenue));
    }

    [Fact]
    public async Task Service_allocation_preserves_every_dong()
    {
        var invoice = Invoice(1, 1);
        Pay(invoice, 1, Start.AddHours(9));

        var report = await Report(invoice);

        Assert.Equal(report.Revenue, report.ByService.Sum(row => row.Revenue));
        Assert.Equal(report.Collected, report.ByService.Sum(row => row.Collected));
    }

    [Fact]
    public async Task Repeated_service_lines_count_the_invoice_once()
    {
        var invoice = SalesInvoice.Create("INV-TEST", "TEN-TEST", "BRN-TEST", "CUS-TEST",
            null, null, "INV-TEST-001", null, "USR-TEST", Start);
        invoice.AddLine("LIN-1", "SVC-TEST", "Dịch vụ", 100_000, 1, Start);
        invoice.AddLine("LIN-2", "SVC-TEST", "Dịch vụ", 100_000, 1, Start);
        Pay(invoice, 200_000, Start.AddHours(9));

        var row = Assert.Single((await Report(invoice)).ByService);

        Assert.Equal(200_000, row.Revenue);
        Assert.Equal(1, row.InvoiceCount);
    }

    [Fact]
    public async Task Tip_on_zero_value_lines_is_reported_as_unallocated_money()
    {
        var invoice = Invoice(0);
        invoice.SetTip(11, Start);
        Pay(invoice, 11, Start.AddHours(9));

        var report = await Report(invoice);

        Assert.Equal(11, report.Collected);
        Assert.Equal(0, report.Revenue);
        Assert.Equal(11, report.ByService.Sum(row => row.Collected));
    }

    private static SalesInvoice Invoice(params long[] prices)
    {
        var invoice = SalesInvoice.Create("INV-TEST", "TEN-TEST", "BRN-TEST", "CUS-TEST",
            null, null, "INV-TEST-001", null, "USR-TEST", Start);
        for (var index = 0; index < prices.Length; index++)
            invoice.AddLine($"LIN-{index}", null, $"Dịch vụ {index}", prices[index], 1, Start);
        return invoice;
    }

    private static void Pay(SalesInvoice invoice, long amount, DateTimeOffset paidAt)
        => invoice.RegisterPayment($"PAY-{invoice.Payments.Count}", PaymentType.Payment,
            PaymentMethod.Cash, amount, paidAt, null, "USR-TEST", paidAt);

    private static Task<RevenueReportDto> Report(SalesInvoice invoice,
        DateTimeOffset? from = null, DateTimeOffset? to = null)
        => new GetRevenueReportUseCase(new RevenueRepository(invoice), new FixedClock())
            .ExecuteAsync(from ?? Start, to ?? Start.AddDays(3), null);

    private sealed class RevenueRepository(SalesInvoice invoice) : IRevenueRepository
    {
        public Task<IReadOnlyList<SalesInvoice>> ListCollectedBetweenAsync(
            DateTimeOffset from, DateTimeOffset to, string? branchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SalesInvoice>>([invoice]);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Start;
    }
}
