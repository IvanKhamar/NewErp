namespace NewErp.Domain.Reports;

/// <summary>Як групувати продажі у звіті (фільтр «Групувати за» у звіті продажів старого сайту).</summary>
public enum ReportGrouping
{
    Day,
    Week,
    Month,
    Year,
}

/// <summary>Одне замовлення для звіту: дата, сума й скільки одиниць товару в ньому.</summary>
public sealed record SaleFact(DateTime DateAdded, decimal Total, int Products);

/// <summary>Рядок звіту: період [Start; End] і що в ньому продано.</summary>
public sealed record SalesPeriod(DateOnly Start, DateOnly End, int Orders, int Products, decimal Total);

/// <summary>Звіт продажів за періодами, як «Звіти → Продажі» старого сайту.</summary>
public static class SalesReport
{
    /// <summary>Перший день періоду, до якого належить дата. Тиждень починається з понеділка, як в Україні (MySQL WEEK() у старому звіті рахував з неділі).</summary>
    public static DateOnly PeriodStart(DateOnly date, ReportGrouping grouping) => grouping switch
    {
        ReportGrouping.Day => date,
        ReportGrouping.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        ReportGrouping.Month => new DateOnly(date.Year, date.Month, 1),
        ReportGrouping.Year => new DateOnly(date.Year, 1, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(grouping)),
    };

    private static DateOnly NextPeriod(DateOnly start, ReportGrouping grouping) => grouping switch
    {
        ReportGrouping.Day => start.AddDays(1),
        ReportGrouping.Week => start.AddDays(7),
        ReportGrouping.Month => start.AddMonths(1),
        _ => start.AddYears(1),
    };

    /// <summary>
    /// Групує замовлення за періодами в межах [from; to]. Межі крайніх періодів обрізаються до from і to.
    /// Порожні періоди старий звіт не показував; для графіка їх можна додати з нулями (fillGaps).
    /// </summary>
    public static IReadOnlyList<SalesPeriod> Group(IEnumerable<SaleFact> facts, ReportGrouping grouping, DateOnly from, DateOnly to, bool fillGaps)
    {
        if (to < from)
            return [];
        var buckets = facts
            .Select(f => (Date: DateOnly.FromDateTime(f.DateAdded), Fact: f))
            .Where(x => x.Date >= from && x.Date <= to)
            .GroupBy(x => PeriodStart(x.Date, grouping))
            .ToDictionary(g => g.Key, g => (Orders: g.Count(), Products: g.Sum(x => x.Fact.Products), Total: g.Sum(x => x.Fact.Total)));

        var rows = new List<SalesPeriod>();
        for (var start = PeriodStart(from, grouping); start <= to; start = NextPeriod(start, grouping))
        {
            var end = NextPeriod(start, grouping).AddDays(-1);
            var clipped = (Start: start < from ? from : start, End: end > to ? to : end);
            if (buckets.TryGetValue(start, out var b))
                rows.Add(new SalesPeriod(clipped.Start, clipped.End, b.Orders, b.Products, b.Total));
            else if (fillGaps)
                rows.Add(new SalesPeriod(clipped.Start, clipped.End, 0, 0, 0));
        }
        return rows;
    }

    /// <summary>Зміна у відсотках відносно попереднього значення; null, якщо порівнювати немає з чим (було 0).</summary>
    public static int? ChangePercent(decimal current, decimal previous) =>
        previous == 0 ? null : (int)Math.Round((current - previous) * 100 / previous, MidpointRounding.AwayFromZero);
}
