using NewErp.Domain.Reports;

namespace NewErp.UnitTests.Domain;

public class SalesReportTests
{
    private static readonly DateOnly From = new(2026, 10, 1), To = new(2026, 10, 20);

    private static readonly SaleFact[] Facts =
    [
        new(new DateTime(2026, 10, 1, 9, 0, 0), 1000m, 2), // четвер
        new(new DateTime(2026, 10, 5, 23, 59, 0), 500m, 1), // понеділок
        new(new DateTime(2026, 10, 11, 12, 0, 0), 300m, 1), // неділя
        new(new DateTime(2026, 10, 21, 0, 0, 0), 999m, 9), // поза періодом
    ];

    [Fact]
    public void Week_StartsOnMonday_EdgesClippedToRange()
    {
        var rows = SalesReport.Group(Facts, ReportGrouping.Week, From, To, fillGaps: false);

        Assert.Equal(
            [(From, new DateOnly(2026, 10, 4), 1, 2, 1000m), (new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 11), 2, 2, 800m)],
            rows.Select(r => (r.Start, r.End, r.Orders, r.Products, r.Total)));
    }

    [Fact]
    public void FillGaps_AddsEmptyDays_ForChart()
    {
        var rows = SalesReport.Group(Facts, ReportGrouping.Day, From, To, fillGaps: true);

        Assert.Equal(20, rows.Count);
        Assert.Equal((0, 0m), (rows[1].Orders, rows[1].Total));
        Assert.Equal(1500m, rows.Sum(r => r.Total) - 300m);
    }

    [Fact]
    public void Month_And_Year_Group()
    {
        Assert.Equal(1800m, Assert.Single(SalesReport.Group(Facts, ReportGrouping.Month, From, To, false)).Total);
        Assert.Equal(new DateOnly(2026, 1, 1), SalesReport.PeriodStart(new DateOnly(2026, 7, 3), ReportGrouping.Year));
        Assert.Empty(SalesReport.Group(Facts, ReportGrouping.Day, To, From, false));
    }

    [Theory]
    [InlineData(150, 100, 50)]
    [InlineData(50, 100, -50)]
    [InlineData(1, 3, -67)]
    public void ChangePercent_Rounded(int current, int previous, int expected) =>
        Assert.Equal(expected, SalesReport.ChangePercent(current, previous));

    [Fact]
    public void ChangePercent_NullWhenNothingBefore() => Assert.Null(SalesReport.ChangePercent(5, 0));
}
