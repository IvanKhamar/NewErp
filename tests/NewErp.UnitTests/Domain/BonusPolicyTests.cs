using NewErp.Domain.Common;
using NewErp.Domain.Staff;

namespace NewErp.UnitTests.Domain;

public class BonusPolicyTests
{
    private const int Completed = 5;
    private const int Cancelled = 7;

    [Fact]
    public void Calculate_CountsOnlyOrdersInCountedStatuses()
    {
        var policy = new BonusPolicy(percent: 2, countedStatusIds: [Completed]);
        BonusOrder[] orders =
        [
            new(1, ManagerId: 1, Completed, 1000m),
            new(2, ManagerId: 1, Cancelled, 5000m),
            new(3, ManagerId: 1, Completed, 500m),
        ];

        var result = Assert.Single(policy.Calculate(orders));

        Assert.Equal(new ManagerBonus(1, OrdersCount: 2, Revenue: 1500m, Bonus: 30m), result);
    }

    [Fact]
    public void Calculate_GroupsByManager_SortedByBonusDescending()
    {
        var policy = new BonusPolicy(1, [Completed]);
        BonusOrder[] orders = [new(1, 1, Completed, 100m), new(2, 2, Completed, 900m), new(3, 2, Completed, 100m)];

        var result = policy.Calculate(orders);

        Assert.Equal([2, 1], result.Select(r => r.ManagerId));
        Assert.Equal(10m, result[0].Bonus);
    }

    [Fact]
    public void Calculate_RoundsToKopecks()
    {
        var policy = new BonusPolicy(1.5m, [Completed]);

        var result = Assert.Single(policy.Calculate([new BonusOrder(1, 1, Completed, 333.33m)]));

        Assert.Equal(5.00m, result.Bonus); // 4.99995 -> 5.00
    }

    [Fact]
    public void Calculate_NoOrders_ReturnsEmpty() =>
        Assert.Empty(new BonusPolicy(1, [Completed]).Calculate([]));

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Ctor_PercentOutOfRange_Throws(decimal percent) =>
        Assert.Throws<DomainException>(() => new BonusPolicy(percent, [Completed]));

    [Fact]
    public void Ctor_NoStatuses_Throws() =>
        Assert.Throws<DomainException>(() => new BonusPolicy(1, []));
}