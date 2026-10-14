using NewErp.Domain.Common;
using NewErp.Domain.Stock;

namespace NewErp.UnitTests.Domain;

public class StockItemTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0);

    private static StockItem NewItem(decimal min = 10) => new(categoryId: 1, supplierId: null, "Льон", "м", min);

    [Fact]
    public void Adjust_Income_IncreasesQuantityAndReturnsMovement()
    {
        var item = NewItem();

        var movement = item.Adjust(25.5m, "Прихід від постачальника", userId: 1, Now);

        Assert.Equal(25.5m, item.Quantity);
        Assert.Equal(25.5m, movement.Delta);
        Assert.Equal(25.5m, movement.QuantityAfter);
        Assert.Equal("Прихід від постачальника", movement.Reason);
        Assert.Same(item, movement.Item);
    }

    [Fact]
    public void Adjust_ConsumeMoreThanAvailable_ThrowsAndKeepsQuantity()
    {
        var item = NewItem();
        item.Adjust(5, "Прихід", 1, Now);

        Assert.Throws<DomainException>(() => item.Adjust(-6, "Пошиття", 1, Now));
        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void Adjust_Zero_Throws() =>
        Assert.Throws<DomainException>(() => NewItem().Adjust(0, "?", 1, Now));

    [Fact]
    public void Adjust_EmptyReason_Throws() =>
        Assert.Throws<DomainException>(() => NewItem().Adjust(1, "  ", 1, Now));

    [Fact]
    public void IsBelowMinimum_TrueUntilMinimumReached()
    {
        var item = NewItem(min: 10);
        Assert.True(item.IsBelowMinimum);

        item.Adjust(10, "Прихід", 1, Now);
        Assert.False(item.IsBelowMinimum);
    }

    [Fact]
    public void Ctor_TrimsAndValidatesName()
    {
        Assert.Equal("Льон", new StockItem(1, null, "  Льон ", "м", 0).Name);
        Assert.Throws<DomainException>(() => new StockItem(1, null, "", "м", 0));
        Assert.Throws<DomainException>(() => new StockItem(1, null, new string('x', 151), "м", 0));
        Assert.Throws<DomainException>(() => new StockItem(1, null, "Льон", "м", -1));
    }
}