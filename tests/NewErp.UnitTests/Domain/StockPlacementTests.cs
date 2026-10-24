using NewErp.Domain.Common;
using NewErp.Domain.Stock;

namespace NewErp.UnitTests.Domain;

public class StockPlacementTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 9, 0, 0);
    private const int Podil = 7;
    private const int Lviv = 8;

    /// <summary>Загалом 30 м: 10 на складі на Подолі, решта 20 — основний склад.</summary>
    private static StockPlacement Placement(out StockItem item)
    {
        item = new StockItem(1, null, "Льон", "м", 0);
        item.Adjust(30, "Прихід", 1, "sklad", Now);
        return new StockPlacement(item, [new StockBalance(0, Podil, 10)]);
    }

    [Fact]
    public void Main_IsTotalMinusWarehouses()
    {
        var placement = Placement(out _);

        Assert.Equal((20m, 10m, 0m), (placement.Main, placement.At(Podil), placement.At(Lviv)));
    }

    [Fact]
    public void Move_ToNewWarehouse_CreatesBalance_TotalStays()
    {
        var placement = Placement(out var item);

        var transfer = placement.Move(null, Lviv, 5, " на продаж ", 1, "sklad", Now);

        Assert.Equal((15m, 5m, 30m), (placement.Main, placement.At(Lviv), item.Quantity));
        Assert.Equal((null, Lviv, 5m, "на продаж"), (transfer.FromWarehouseId, transfer.ToWarehouseId, transfer.Quantity, transfer.Comment));
        Assert.Equal(Lviv, Assert.Single(placement.Created).WarehouseId);
    }

    [Fact]
    public void Move_MoreThanSourceHas_OrToSamePlace_Rejected()
    {
        var placement = Placement(out _);

        var tooMuch = Assert.Throws<DomainException>(() => placement.Move(Podil, null, 11, null, 1, "sklad", Now));
        Assert.Contains("на цьому складі є 10 м", tooMuch.Message);
        Assert.Throws<DomainException>(() => placement.Move(Podil, Podil, 1, null, 1, "sklad", Now));
        Assert.Throws<DomainException>(() => placement.Move(Podil, null, 0.0001m, null, 1, "sklad", Now));
    }

    [Fact]
    public void AdjustAt_Main_CannotUseWhatLiesAtWarehouses()
    {
        var placement = Placement(out var item);

        Assert.Throws<DomainException>(() => placement.AdjustAt(null, -25, "Пошиття", 1, "sklad", Now));

        var movement = placement.AdjustAt(Podil, -4, "Брак", 1, "sklad", Now);
        Assert.Equal((-4m, 26m, 6m, 20m), (movement.Delta, item.Quantity, placement.At(Podil), placement.Main));
    }

    [Fact]
    public void ReceivedAt_PutsDeliveryOnWarehouse()
    {
        var placement = Placement(out var item);
        item.Adjust(12, "Поставка", 1, "sklad", Now); // приймання вже збільшило загальний залишок

        placement.ReceivedAt(Podil, 12);

        Assert.Equal((22m, 20m), (placement.At(Podil), placement.Main));
    }
}
