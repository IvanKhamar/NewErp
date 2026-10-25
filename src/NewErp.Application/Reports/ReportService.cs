using NewErp.Domain.Common;
using NewErp.Domain.Orders;
using NewErp.Domain.Reports;

namespace NewErp.Application.Reports;

/// <summary>
/// Зведення на головній і звіти продажів і товарів, як головна сторінка і «Звіти» старого сайту.
/// Скасовані замовлення («Відмінено») не лічаться, якщо статус не вибрано явно; старий сайт їх лічив.
/// </summary>
public sealed class ReportService(IReportStore store, TimeProvider clock)
{
    public const int MaxDays = 3 * 366;
    public const int MaxProducts = 200;

    public async Task<SalesReportDto> SalesAsync(ReportFilter filter, CancellationToken ct)
    {
        var (from, to) = Range(filter);
        var facts = await store.SalesAsync(Start(from), Start(to.AddDays(1)), filter.StatusId, CustomerRisk.CancelledStatusId, ct);
        var rows = SalesReport.Group(facts, filter.Group, from, to, fillGaps: false).Select(ToRow).ToList();
        return new SalesReportDto(from, to, rows, rows.Sum(r => r.Orders), rows.Sum(r => r.Products), rows.Sum(r => r.Total));
    }

    public async Task<ProductSalesDto> ProductsAsync(ReportFilter filter, CancellationToken ct)
    {
        var (from, to) = Range(filter);
        var rows = await store.ProductsAsync(Start(from), Start(to.AddDays(1)), filter.StatusId, CustomerRisk.CancelledStatusId, MaxProducts, ct);
        return new ProductSalesDto(from, to, rows);
    }

    /// <summary>Замовлення й повернення за статусами; StatusId і Group фільтра тут не діють.</summary>
    public async Task<StatusReportDto> StatusesAsync(ReportFilter filter, CancellationToken ct)
    {
        var (from, to) = Range(filter);
        var orders = await store.OrdersByStatusAsync(Start(from), Start(to.AddDays(1)), ct);
        var returns = await store.ReturnsByStatusAsync(Start(from), Start(to.AddDays(1)), ct);
        return new StatusReportDto(from, to, orders, orders.Sum(r => r.Orders), orders.Sum(r => r.Total), returns, returns.Sum(r => r.Returns));
    }

    public async Task<DashboardDto> DashboardAsync(CancellationToken ct)
    {
        var today = Today();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        // Місяць порівнюємо з тими самими днями попереднього: 1–20 жовтня проти 1–20 вересня.
        var prevMonthStart = monthStart.AddMonths(-1);
        var prevMonthEnd = DateOnly.FromDayNumber(Math.Min(prevMonthStart.AddDays(today.Day - 1).DayNumber, monthStart.AddDays(-1).DayNumber));
        var chartFrom = today.AddDays(-29);

        var earliest = new[] { prevMonthStart, chartFrom }.Min();
        var facts = await store.SalesAsync(Start(earliest), Start(today.AddDays(1)), null, CustomerRisk.CancelledStatusId, ct);
        (int Orders, decimal Total) Sum(DateOnly from, DateOnly to) => Totals(facts, from, to);

        var day = Sum(today, today);
        var yesterday = Sum(today.AddDays(-1), today.AddDays(-1));
        var month = Sum(monthStart, today);
        var prevMonth = Sum(prevMonthStart, prevMonthEnd);
        var customers = await store.NewCustomersAsync(Start(monthStart), Start(today.AddDays(1)), ct);
        var prevCustomers = await store.NewCustomersAsync(Start(prevMonthStart), Start(prevMonthEnd.AddDays(1)), ct);
        var top = await store.ProductsAsync(Start(chartFrom), Start(today.AddDays(1)), null, CustomerRisk.CancelledStatusId, 5, ct);

        return new DashboardDto(
            Tile(day.Orders, yesterday.Orders),
            Tile(day.Total, yesterday.Total),
            Tile(month.Orders, prevMonth.Orders),
            Tile(month.Total, prevMonth.Total),
            Tile(Average(month), Average(prevMonth)),
            Tile(customers, prevCustomers),
            SalesReport.Group(facts, ReportGrouping.Day, chartFrom, today, fillGaps: true).Select(ToRow).ToList(),
            top);
    }

    /// <summary>Фід виручки: сьогодні, вчора, місяць і рік дотепер і останні <paramref name="days"/> днів по одному.</summary>
    public async Task<RevenueFeedDto> RevenueFeedAsync(int days, int maxDays, CancellationToken ct)
    {
        if (days < 1 || days > maxDays)
            throw new DomainException($"Кількість днів — від 1 до {maxDays}.");
        var today = Today();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);
        var prevMonthEnd = DateOnly.FromDayNumber(Math.Min(prevMonthStart.AddDays(today.Day - 1).DayNumber, monthStart.AddDays(-1).DayNumber));
        var yearStart = new DateOnly(today.Year, 1, 1);
        var seriesFrom = today.AddDays(1 - days);

        var from = new[] { prevMonthStart, yearStart, seriesFrom }.Min();
        var facts = await store.SalesAsync(Start(from), Start(today.AddDays(1)), null, CustomerRisk.CancelledStatusId, ct);
        RevenueTotals Feed(DateOnly start, DateOnly end)
        {
            var sum = Totals(facts, start, end);
            return new RevenueTotals(start, end, sum.Orders, sum.Total, Average(sum));
        }

        var series = SalesReport.Group(facts, ReportGrouping.Day, seriesFrom, today, fillGaps: true)
            .Select(p => new RevenueDay(p.Start, p.Orders, p.Products, p.Total))
            .ToList();
        return new RevenueFeedDto(
            clock.GetLocalNow().DateTime, "UAH", Feed(today, today), Feed(today.AddDays(-1), today.AddDays(-1)),
            Feed(monthStart, today), Feed(prevMonthStart, prevMonthEnd), Feed(yearStart, today), series);
    }

    private static (int Orders, decimal Total) Totals(IReadOnlyList<SaleFact> facts, DateOnly from, DateOnly to)
    {
        var list = facts.Where(f => DateOnly.FromDateTime(f.DateAdded) >= from && DateOnly.FromDateTime(f.DateAdded) <= to).ToList();
        return (list.Count, list.Sum(f => f.Total));
    }

    private (DateOnly From, DateOnly To) Range(ReportFilter filter)
    {
        var today = Today();
        // Типово — поточний місяць, як у старих звітах.
        var from = filter.From ?? new DateOnly(today.Year, today.Month, 1);
        var to = filter.To ?? today;
        if (to < from)
            throw new DomainException("Дата «по» раніша за дату «з».");
        if (to.DayNumber - from.DayNumber > MaxDays)
            throw new DomainException("Період звіту — не більше трьох років.");
        return (from, to);
    }

    private static decimal Average((int Orders, decimal Total) s) => s.Orders == 0 ? 0 : Math.Round(s.Total / s.Orders, 2);

    private static DashboardTile Tile(decimal current, decimal previous) => new(current, previous, SalesReport.ChangePercent(current, previous));

    private static SalesReportRow ToRow(SalesPeriod p) => new(p.Start, p.End, p.Orders, p.Products, p.Total);

    private static DateTime Start(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}
