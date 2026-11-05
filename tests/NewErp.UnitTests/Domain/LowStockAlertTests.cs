using NewErp.Domain.Production;
using NewErp.Domain.Stock;

namespace NewErp.UnitTests.Domain;

public class LowStockAlertTests
{
    private static LowStockLine Line(string name, decimal quantity = 2, decimal minimum = 10) => new(name, quantity, minimum, "м");

    [Fact]
    public void Text_ListsTheItems() =>
        Assert.Equal("Мало на складі: Льон 12 м (мін. 30); Нитки 3 м (мін. 10)", LowStockAlert.Text([Line("Льон", 12, 30), Line("Нитки", 3)], 670));

    [Fact]
    public void Text_CountsWhatDoesNotFit()
    {
        var text = LowStockAlert.Text(Enumerable.Range(0, 40).Select(n => Line($"Тканина {n:00}")).ToList(), 120);

        Assert.True(text.Length <= 120);
        Assert.EndsWith(" поз.", text);
        Assert.StartsWith("Мало на складі: Тканина 00 2 м (мін. 10); Тканина 01", text);
    }
}

public class SpreadByShareTests
{
    [Fact]
    public void RoundsUp_BySharesInABatch()
    {
        var pieces = MaterialNeeds.SpreadByShare(10, new Dictionary<int, decimal> { [1] = 30, [2] = 70 });

        Assert.Equal((3m, 7m), (pieces[1], pieces[2]));
        Assert.Equal(4m, MaterialNeeds.SpreadByShare(11, new Dictionary<int, decimal> { [1] = 30, [2] = 70 })[1]); // 3.3 → 4
    }

    [Fact]
    public void WithoutShares_SplitsEvenly()
    {
        var pieces = MaterialNeeds.SpreadByShare(9, new Dictionary<int, decimal> { [1] = 0, [2] = 0 });

        Assert.Equal((5m, 5m), (pieces[1], pieces[2]));
    }

    [Fact]
    public void ASizeWithoutAShare_GetsNothing_WhenOthersHaveOne() =>
        Assert.Equal([2], MaterialNeeds.SpreadByShare(5, new Dictionary<int, decimal> { [1] = 0, [2] = 100 }).Keys);

    [Fact]
    public void NothingLacking_IsEmpty()
    {
        Assert.Empty(MaterialNeeds.SpreadByShare(0, new Dictionary<int, decimal> { [1] = 50 }));
        Assert.Empty(MaterialNeeds.SpreadByShare(-3, new Dictionary<int, decimal> { [1] = 50 }));
        Assert.Empty(MaterialNeeds.SpreadByShare(5, new Dictionary<int, decimal>()));
    }
}
