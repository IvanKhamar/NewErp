using NewErp.Domain.Common;
using NewErp.Domain.Customers;

namespace NewErp.UnitTests.Domain;

public class CustomerSummaryTests
{
    [Fact]
    public void Spent_ExcludesCancelled_DatesSpanAllOrders()
    {
        var summary = CustomerSummary.From([
            new(1, new DateTime(2026, 1, 5), 22, 1500m),
            new(2, new DateTime(2026, 3, 1), 19, 700m),
            new(3, new DateTime(2026, 2, 10), 24, 900m),
        ]);

        Assert.Equal((3, 1, 2400m, new DateTime(2026, 1, 5), new DateTime(2026, 3, 1)),
            (summary.OrderCount, summary.CancelledCount, summary.Spent, summary.FirstOrder, summary.LastOrder));
    }

    [Fact]
    public void NoOrders_NoDates()
    {
        var summary = CustomerSummary.From([]);
        Assert.Equal((0, 0m, (DateTime?)null), (summary.OrderCount, summary.Spent, summary.LastOrder));
    }

    [Fact]
    public void Note_IsTrimmed_EmptyAndTooLongRejected()
    {
        Assert.Equal("Просила дзвонити після 18:00", CustomerNote.Validate("  Просила дзвонити після 18:00 "));
        Assert.Throws<DomainException>(() => CustomerNote.Validate("   "));
        Assert.Throws<DomainException>(() => CustomerNote.Validate(new string('я', CustomerNote.MaxLength + 1)));
    }
}
