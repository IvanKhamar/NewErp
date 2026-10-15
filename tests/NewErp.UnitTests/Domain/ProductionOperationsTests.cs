using NewErp.Domain.Common;
using NewErp.Domain.Production;

namespace NewErp.UnitTests.Domain;

public class ProductionOperationsTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private static (ProductionModel Model, int SizeId) NewModel()
    {
        var model = new ProductionModel("Сукня", ProductionModel.Clothing, 0, 0);
        model.SetSizes([("M", 100m)]);
        var size = model.Sizes[0];
        typeof(ProductionSize).GetProperty(nameof(ProductionSize.Id))!.SetValue(size, 7);
        return (model, 7);
    }

    [Fact]
    public void BomResolver_SizeSpecificRowOverridesGeneralOne()
    {
        (int? Size, int Item, decimal Qty)[] rows = [(null, 1, 1.2m), (5, 1, 1.5m), (null, 2, 3m), (6, 2, 9m)];

        var forSize5 = BomResolver.Resolve(rows, 5, r => r.Size, r => r.Item);

        Assert.Equal([1.5m, 3m], forSize5.OrderBy(r => r.Item).Select(r => r.Qty));
    }

    [Fact]
    public void RecordCut_IncreasesCutStockAndWritesLogLikeOldModule()
    {
        var (model, sizeId) = NewModel();

        var entry = model.RecordCut(sizeId, 4, " партія 12 ", 3, "sklad", Now);

        Assert.Equal(4, model.Sizes[0].CutStock);
        Assert.Equal((ProductionLogEntry.Cut, ProductionLogEntry.In, 4m), (entry.Stage, entry.Direction, entry.Quantity));
        Assert.Equal((0m, 4m, "M", "партія 12"), (entry.StockBefore, entry.StockAfter, entry.SizeName, entry.Comment));
    }

    [Fact]
    public void SendToSewing_CannotHandOverMoreThanCut()
    {
        var (model, sizeId) = NewModel();
        model.RecordCut(sizeId, 2, null, 3, "sklad", Now);

        Assert.Throws<DomainException>(() => model.SendToSewing(sizeId, 3, null, 3, "sklad", Now));

        var entry = model.SendToSewing(sizeId, 2, null, 3, "sklad", Now);
        Assert.Equal((ProductionLogEntry.Out, 0m), (entry.Direction, model.Sizes[0].CutStock));
    }

    [Fact]
    public void Ship_CannotShipMoreThanSewn()
    {
        var (model, sizeId) = NewModel();
        model.RecordSewn(sizeId, 1, null, 3, "sklad", Now);

        Assert.Throws<DomainException>(() => model.Ship(sizeId, 2, null, 3, "sklad", Now));
    }

    [Fact]
    public void Operations_RejectNonPositiveQuantityAndForeignSize()
    {
        var (model, sizeId) = NewModel();

        Assert.Throws<DomainException>(() => model.RecordCut(sizeId, 0, null, 3, "sklad", Now));
        Assert.Throws<DomainException>(() => model.RecordSewn(99, 1, null, 3, "sklad", Now));
    }

    [Fact]
    public void ComponentStock_TakeUpTo_TakesWhatIsThere()
    {
        var stock = new ComponentStock(1, 7);
        stock.Add(2);

        Assert.Equal(2, stock.TakeUpTo(3));
        Assert.Equal(0, stock.Stock);
    }
}
