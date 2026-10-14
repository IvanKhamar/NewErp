using NewErp.Domain.Common;
using NewErp.Domain.Production;

namespace NewErp.UnitTests.Domain;

public class BomTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 10, 0, 0);

    [Fact]
    public void SetLines_ReplacesUpdatesAndRemoves()
    {
        var bom = new Bom(101);
        bom.SetLines([(1, 2.5m), (2, 6)], userId: 1, Now);

        bom.SetLines([(2, 4), (3, 1)], userId: 2, Now.AddDays(1));

        Assert.Equal([(2, 4m), (3, 1m)], bom.Lines.Select(l => (l.StockItemId, l.Quantity)).OrderBy(x => x.StockItemId));
        Assert.All(bom.Lines, l => Assert.Equal(101, l.ProductId));
        Assert.Equal(2, bom.UpdatedByUserId);
    }

    [Fact]
    public void SetLines_KeepsSameLineObjectWhenOnlyQuantityChanges()
    {
        var bom = new Bom(101);
        bom.SetLines([(1, 2)], userId: 1, Now);
        var line = bom.Lines[0];

        bom.SetLines([(1, 3)], userId: 1, Now);

        Assert.Same(line, bom.Lines[0]);
        Assert.Equal(3, line.Quantity);
    }

    [Fact]
    public void SetLines_Empty_Rejected() =>
        Assert.Throws<DomainException>(() => new Bom(101).SetLines([], 1, Now));

    [Fact]
    public void SetLines_DuplicateItem_Rejected() =>
        Assert.Throws<DomainException>(() => new Bom(101).SetLines([(1, 1), (1, 2)], 1, Now));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetLines_NonPositiveQuantity_Rejected(decimal quantity) =>
        Assert.Throws<DomainException>(() => new Bom(101).SetLines([(1, 1), (2, quantity)], 1, Now));

    [Fact]
    public void Ctor_NonPositiveProductId_Rejected() =>
        Assert.Throws<DomainException>(() => new Bom(0));
}
