using NewErp.Domain.Common;
using NewErp.Domain.Production;

namespace NewErp.UnitTests.Domain;

public class SewnPlacementTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 10, 0, 0);
    private const int SizeId = 7;
    private const int Podil = 3;

    /// <summary>Розмір M: пошито 10, з них 4 на складі на Подолі.</summary>
    private static SewnPlacement Placement(out ProductionModel model)
    {
        model = new ProductionModel("Сукня", ProductionModel.Clothing, 0, 0);
        model.SetSizes([("M", 100m)]);
        typeof(ProductionSize).GetProperty(nameof(ProductionSize.Id))!.SetValue(model.Sizes[0], SizeId);
        model.RecordSewn(SizeId, 10, null, 1, "sklad", Now);
        return new SewnPlacement(model, [new SewnBalance(SizeId, Podil, 4)]);
    }

    [Fact]
    public void SewnAt_Warehouse_GrowsSewnStockAndThatWarehouse()
    {
        var placement = Placement(out var model);

        var entry = placement.SewnAt(SizeId, Podil, 2, null, 1, "sklad", Now);

        Assert.Equal((12m, 6m, 6m), (model.Sizes[0].SewnStock, placement.At(SizeId, Podil), placement.At(SizeId, null)));
        Assert.Equal(2, entry.Quantity);
    }

    [Fact]
    public void ShipFrom_Main_CannotTakeWhatLiesAtWarehouse()
    {
        var placement = Placement(out var model);

        var error = Assert.Throws<DomainException>(() => placement.ShipFrom(SizeId, null, 7, null, 1, "sklad", Now));

        Assert.Contains("на основному складі пошито 6", error.Message);
        Assert.Equal(10, model.Sizes[0].SewnStock);
        placement.ShipFrom(SizeId, Podil, 4, null, 1, "sklad", Now);
        Assert.Equal((6m, 0m), (model.Sizes[0].SewnStock, placement.At(SizeId, Podil)));
    }

    [Fact]
    public void Move_KeepsSewnStock_CreatesBalanceForNewWarehouse()
    {
        var placement = Placement(out var model);

        var transfer = placement.Move(SizeId, null, 9, 5, "у шоурум", 1, "sklad", Now);

        Assert.Equal((10m, 1m, 5m), (model.Sizes[0].SewnStock, placement.At(SizeId, null), placement.At(SizeId, 9)));
        Assert.Equal((SizeId, (int?)null, (int?)9), (transfer.SizeId, transfer.FromWarehouseId, transfer.ToWarehouseId));
        Assert.Equal(9, Assert.Single(placement.Created).WarehouseId);
        Assert.Throws<DomainException>(() => placement.Move(SizeId, Podil, null, 5, null, 1, "sklad", Now));
    }
}
