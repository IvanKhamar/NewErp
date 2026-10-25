using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Reports;
using NewErp.Domain.Common;
using NewErp.Domain.Reports;

namespace NewErp.UnitTests.Application;

public class ReportServiceTests
{
    private readonly FakeReportStore _store = new();
    private readonly ReportService _service;

    public ReportServiceTests()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 20, 15, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _service = new ReportService(_store, clock);
        _store.Facts =
        [
            new(new DateTime(2026, 10, 20, 10, 0, 0), 1500m, 2),
            new(new DateTime(2026, 10, 19, 10, 0, 0), 1000m, 1),
            new(new DateTime(2026, 10, 2, 10, 0, 0), 500m, 1),
            new(new DateTime(2026, 9, 10, 10, 0, 0), 2000m, 3),
            new(new DateTime(2026, 9, 25, 10, 0, 0), 9000m, 9), // після 20 вересня: у порівняння місяців не входить
        ];
    }

    [Fact]
    public async Task Dashboard_ComparesTodayWithYesterday_AndMonthWithSameDaysOfPreviousMonth()
    {
        var d = await _service.DashboardAsync(default);

        Assert.Equal((1m, 1m, 0), (d.OrdersToday.Current, d.OrdersToday.Previous, d.OrdersToday.ChangePercent));
        Assert.Equal((1500m, 1000m, 50), (d.SalesToday.Current, d.SalesToday.Previous, d.SalesToday.ChangePercent));
        Assert.Equal((3000m, 2000m), (d.SalesMonth.Current, d.SalesMonth.Previous));
        Assert.Equal((1000m, 2000m, -50), (d.AverageCheckMonth.Current, d.AverageCheckMonth.Previous, d.AverageCheckMonth.ChangePercent));
        Assert.Equal(30, d.Last30Days.Count);
        Assert.Equal((new DateTime(2026, 9, 1), new DateTime(2026, 10, 21)), _store.LastRange);
    }

    [Fact]
    public async Task Sales_DefaultsToCurrentMonth_RejectsBadRange()
    {
        var report = await _service.SalesAsync(new ReportFilter(Group: ReportGrouping.Month), default);

        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20), 3, 3000m), (report.From, report.To, report.Orders, report.Total));
        await Assert.ThrowsAsync<DomainException>(() => _service.SalesAsync(new ReportFilter(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 1)), default));
        await Assert.ThrowsAsync<DomainException>(() => _service.SalesAsync(new ReportFilter(new DateOnly(2020, 1, 1)), default));
    }

    [Fact]
    public async Task RevenueFeed_TotalsAndDailySeries_RejectsTooManyDays()
    {
        var feed = await _service.RevenueFeedAsync(days: 3, maxDays: 366, default);

        Assert.Equal((1, 1500m, 1500m), (feed.Today.Orders, feed.Today.Revenue, feed.Today.AverageCheck));
        Assert.Equal((new DateOnly(2026, 10, 1), 3000m), (feed.Month.From, feed.Month.Revenue));
        Assert.Equal((new DateOnly(2026, 9, 20), 2000m), (feed.PreviousMonth.To, feed.PreviousMonth.Revenue));
        Assert.Equal(14000m, feed.Year.Revenue);
        Assert.Equal([0m, 1000m, 1500m], feed.Days.Select(d => d.Revenue));
        Assert.Equal("UAH", feed.Currency);
        await Assert.ThrowsAsync<DomainException>(() => _service.RevenueFeedAsync(400, 366, default));
    }

    [Fact]
    public async Task Statuses_SumTheRows_ForTheCurrentMonthByDefault()
    {
        _store.ByStatus = [new(24, "1) Уточнюємо", 3, 3600m), new(19, "Відмінено", 1, 700m)];
        _store.ReturnsByStatus = [new(1, "Очікує", 2, 3)];

        var report = await _service.StatusesAsync(new ReportFilter(), default);

        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20)), (report.From, report.To));
        Assert.Equal((new DateTime(2026, 10, 1), new DateTime(2026, 10, 21)), _store.LastRange);
        Assert.Equal((4, 4300m, 2), (report.OrderCount, report.Total, report.ReturnCount));
        await Assert.ThrowsAsync<DomainException>(() => _service.StatusesAsync(new ReportFilter(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 1)), default));
    }

    private sealed class FakeReportStore : IReportStore
    {
        public IReadOnlyList<SaleFact> Facts { get; set; } = [];
        public (DateTime, DateTime) LastRange { get; private set; }

        public Task<IReadOnlyList<SaleFact>> SalesAsync(DateTime from, DateTime toExclusive, int? statusId, int excludedStatusId, CancellationToken ct)
        {
            LastRange = (from, toExclusive);
            return Task.FromResult<IReadOnlyList<SaleFact>>(Facts.Where(f => f.DateAdded >= from && f.DateAdded < toExclusive).ToList());
        }

        public Task<IReadOnlyList<ProductSalesRow>> ProductsAsync(
            DateTime from, DateTime toExclusive, int? statusId, int excludedStatusId, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProductSalesRow>>([]);

        public Task<int> NewCustomersAsync(DateTime from, DateTime toExclusive, CancellationToken ct) => Task.FromResult(0);

        public IReadOnlyList<OrderStatusReportRow> ByStatus { get; set; } = [];
        public IReadOnlyList<ReturnStatusReportRow> ReturnsByStatus { get; set; } = [];

        public Task<IReadOnlyList<OrderStatusReportRow>> OrdersByStatusAsync(DateTime from, DateTime toExclusive, CancellationToken ct)
        {
            LastRange = (from, toExclusive);
            return Task.FromResult(ByStatus);
        }

        public Task<IReadOnlyList<ReturnStatusReportRow>> ReturnsByStatusAsync(DateTime from, DateTime toExclusive, CancellationToken ct) =>
            Task.FromResult(ReturnsByStatus);
    }
}
