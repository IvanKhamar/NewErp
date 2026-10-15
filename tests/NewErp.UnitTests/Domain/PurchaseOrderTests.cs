using NewErp.Domain.Common;
using NewErp.Domain.Stock;

namespace NewErp.UnitTests.Domain;

public class PurchaseOrderTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 10, 0, 0);

    private readonly StockItem _linen = Item(1, "Льон", "м");
    private readonly StockItem _zipper = Item(2, "Блискавка", "шт");

    private static StockItem Item(int id, string name, string unit)
    {
        var item = new StockItem(null, 1, name, unit, 0);
        typeof(StockItem).GetProperty(nameof(StockItem.Id))!.SetValue(item, id);
        return item;
    }

    private Dictionary<int, StockItem> Items => new() { [1] = _linen, [2] = _zipper };

    private static PurchaseOrder NewOrder()
    {
        var order = new PurchaseOrder(1, [(1, 30m, 200m), (2, 100m, 5m)], null, 3, Now);
        // Id рядків задає база; у тесті виставляємо, щоб приймати за id рядка.
        for (var i = 0; i < order.Lines.Count; i++)
            typeof(PurchaseOrderLine).GetProperty(nameof(PurchaseOrderLine.Id))!.SetValue(order.Lines[i], i + 1);
        return order;
    }

    [Fact]
    public void Receive_PartiallyThenRest_ClosesOrderAndRestocksItems()
    {
        var order = NewOrder();
        order.MarkOrdered(Now);

        var first = order.Receive([(1, 20m)], Items, 3, "sklad", Now);
        Assert.Equal(PurchaseOrder.Ordered, order.Status);
        Assert.Equal(10, order.Lines[0].Remaining);
        Assert.Equal((StockMovement.Restock, 20m), (Assert.Single(first).Action, _linen.Quantity));

        order.Receive([(1, 11m), (2, 100m)], Items, 3, "sklad", Now); // льону привезли на 1 м більше

        Assert.Equal(PurchaseOrder.Received, order.Status);
        Assert.Equal(31, _linen.Quantity);
        Assert.Equal(Now, order.ClosedAt);
    }

    [Fact]
    public void Receive_OnlyWhenOrdered()
    {
        var order = NewOrder();

        Assert.Throws<DomainException>(() => order.Receive([(1, 1m)], Items, 3, "sklad", Now));
    }

    [Fact]
    public void Receive_RejectsForeignLineAndNonPositiveQuantity()
    {
        var order = NewOrder();
        order.MarkOrdered(Now);

        Assert.Throws<DomainException>(() => order.Receive([(99, 1m)], Items, 3, "sklad", Now));
        Assert.Throws<DomainException>(() => order.Receive([(1, 0m)], Items, 3, "sklad", Now));
    }

    [Fact]
    public void SetLines_OnlyInDraftAndValidated()
    {
        var order = NewOrder();
        Assert.Throws<DomainException>(() => order.SetLines([(1, 1m, 0m), (1, 2m, 0m)]));
        Assert.Throws<DomainException>(() => order.SetLines([(1, 0m, 0m)]));
        Assert.Throws<DomainException>(() => order.SetLines([]));

        order.MarkOrdered(Now);
        Assert.Throws<DomainException>(() => order.SetLines([(1, 5m, 0m)]));
    }

    [Fact]
    public void Cancel_OpenOrderOnly()
    {
        var order = NewOrder();
        order.Cancel(Now);

        Assert.Equal(PurchaseOrder.Cancelled, order.Status);
        Assert.False(order.IsOpen);
        Assert.Throws<DomainException>(() => order.Cancel(Now));
    }
}
