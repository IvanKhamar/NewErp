using NewErp.Domain.Common;
using NewErp.Domain.Stock;

namespace NewErp.UnitTests.Domain;

public class StockItemTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0);

    private static StockItem NewItem(decimal min = 10) => new(categoryId: 1, supplierId: null, "Льон", "м", min);

    [Fact]
    public void Adjust_Income_IncreasesQuantityAndWritesRestockLikeOldLog()
    {
        var item = NewItem();

        var movement = item.Adjust(25.5m, "Прихід від постачальника", userId: 1, "sklad", Now);

        Assert.Equal(25.5m, item.Quantity);
        Assert.Equal(StockMovement.Restock, movement.Action);
        Assert.Equal(25.5m, movement.Quantity);
        Assert.Equal(25.5m, movement.Delta);
        Assert.Equal((0m, 25.5m), (movement.StockBefore, movement.StockAfter));
        Assert.Equal("Прихід від постачальника", movement.Comment);
        Assert.Equal(("Льон", "sklad"), (movement.ItemName, movement.Username));
        Assert.Same(item, movement.Item);
    }

    [Fact]
    public void Adjust_Consumption_WritesUseWithPositiveQuantity()
    {
        var item = NewItem();
        item.Adjust(10, "Прихід", 1, "sklad", Now);

        var movement = item.Adjust(-4, "Пошиття", 1, "sklad", Now);

        Assert.Equal(StockMovement.Use, movement.Action);
        Assert.Equal(4, movement.Quantity);
        Assert.Equal(-4, movement.Delta);
    }

    [Fact]
    public void Adjust_ConsumeMoreThanAvailable_ThrowsAndKeepsQuantity()
    {
        var item = NewItem();
        item.Adjust(5, "Прихід", 1, "sklad", Now);

        Assert.Throws<DomainException>(() => item.Adjust(-6, "Пошиття", 1, "sklad", Now));
        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void TakeUpTo_NotEnough_TakesWhatIsThereLikeOldModule()
    {
        var item = NewItem();
        item.Adjust(3, "Прихід", 1, "sklad", Now);

        var movement = item.TakeUpTo(5, "Крій", 1, "sklad", Now);

        Assert.Equal(0, item.Quantity);
        Assert.Equal(3, movement!.Quantity);
        Assert.Null(item.TakeUpTo(1, "Крій", 1, "sklad", Now)); // порожньо — нічого не списується
    }

    [Fact]
    public void Adjust_Zero_Throws() =>
        Assert.Throws<DomainException>(() => NewItem().Adjust(0, "?", 1, "sklad", Now));

    [Fact]
    public void Adjust_EmptyReason_Throws() =>
        Assert.Throws<DomainException>(() => NewItem().Adjust(1, "  ", 1, "sklad", Now));

    [Fact]
    public void IsBelowMinimum_TrueUntilMinimumReached()
    {
        var item = NewItem(min: 10);
        Assert.True(item.IsBelowMinimum);

        item.Adjust(10, "Прихід", 1, "sklad", Now);
        Assert.False(item.IsBelowMinimum);
    }

    [Fact]
    public void Update_ChangesDescriptionButNotQuantity()
    {
        var item = NewItem();
        item.Adjust(12, "Прихід", 1, "sklad", Now);

        item.Update(categoryId: 2, supplierId: 5, " Льон білий ", "м", 20, "сукні", 240);

        Assert.Equal((2, 5, "Льон білий", 20m, 240m), (item.CategoryId, item.SupplierId, item.Name, item.MinQuantity, item.Price));
        Assert.Equal(12, item.Quantity);
        Assert.Throws<DomainException>(() => item.Update(null, null, "Льон", "м", 0, null, price: -1));
    }

    [Fact]
    public void Ctor_TrimsAndValidates()
    {
        Assert.Equal("Льон", new StockItem(null, null, "  Льон ", "м", 0).Name);
        Assert.Throws<DomainException>(() => new StockItem(1, null, "", "м", 0));
        Assert.Throws<DomainException>(() => new StockItem(1, null, new string('x', 151), "м", 0));
        Assert.Throws<DomainException>(() => new StockItem(1, null, "Льон", "м", -1));
        Assert.Throws<DomainException>(() => new StockItem(1, null, "Льон", "м", 0, price: -1));
    }
}
