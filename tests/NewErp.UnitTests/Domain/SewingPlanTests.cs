using NewErp.Domain.Production;

namespace NewErp.UnitTests.Domain;

public class SewingPlanTests
{
    private const int Size = 7;

    [Fact]
    public void FreeSewnCoversFirst_ThenCut_RestIsToCut()
    {
        var line = SewingPlan.ForSize(Size, sewnStock: 1, cutStock: 2,
            [new SewingDemand(1, Size, 3, 0, null), new SewingDemand(2, Size, 2, 0, null)]);

        Assert.Equal((5m, 5m, 4m, 2m, 2m), (line.Ordered, line.Missing, line.ToSew, line.FromCut, line.ToCut));
    }

    [Fact]
    public void WhatOrderAlreadyHolds_IsNotMissing()
    {
        var line = SewingPlan.ForSize(Size, 0, 0, [new SewingDemand(1, Size, 3, 2, null), new SewingDemand(2, Size, 1, 1, null)]);

        Assert.Equal((4m, 1m, 1m), (line.Ordered, line.Missing, line.ToSew));
        Assert.Equal([1], line.OrderIds);
    }

    [Fact]
    public void EnoughSewn_NothingToSew_NegativeStockIgnored()
    {
        Assert.Equal(0, SewingPlan.ForSize(Size, 5, 0, [new SewingDemand(1, Size, 3, 0, null)]).ToSew);
        Assert.Equal((3m, 0m), SewingPlan.ForSize(Size, -2, -1, [new SewingDemand(1, Size, 3, 0, null)]) is var l ? (l.ToSew, l.FromCut) : default);
    }

    [Fact]
    public void Deadline_IsEarliestAmongOrdersStillMissing_OtherSizesIgnored()
    {
        var line = SewingPlan.ForSize(Size, 0, 0,
        [
            new SewingDemand(1, Size, 1, 1, new DateOnly(2026, 10, 1)), // уже має все
            new SewingDemand(2, Size, 1, 0, new DateOnly(2026, 10, 9)),
            new SewingDemand(3, Size, 1, 0, null),
            new SewingDemand(4, Size + 1, 5, 0, new DateOnly(2026, 9, 1)),
        ]);

        Assert.Equal(new DateOnly(2026, 10, 9), line.Deadline);
        Assert.Equal([2, 3], line.OrderIds);
        Assert.Equal(3, line.Ordered);
    }

    [Fact]
    public void Deadline_IsOrderDatePlusCategoryDays_ZeroMeansNotSet()
    {
        Assert.Equal(new DateOnly(2026, 10, 15), SewingPlan.Deadline(new DateTime(2026, 10, 1, 23, 30, 0), 14));
        Assert.Null(SewingPlan.Deadline(new DateTime(2026, 10, 1), 0));
    }
}
