using NewErp.Application.Stock;

namespace NewErp.UnitTests.Application;

public class ReorderServiceTests
{
    private static readonly Dictionary<int, StockSupplierDto> Suppliers = new()
    {
        [1] = new(1, "Текстиль-Опт", "+380441234567", "opt@example.com", "Тканини", ""),
        [2] = new(2, "Фурнітура", "", "", "", ""),
    };

    private static StockItemDto Item(int id, string name, int? supplierId, decimal quantity, decimal min, decimal price = 0, string unit = "м") =>
        new(id, name, unit, null, null, supplierId, null, quantity, min, quantity < min, "", price);

    [Fact]
    public void Build_SuggestsUpToTwiceMinimumRoundedUpAndGroupsBySupplier()
    {
        StockItemDto[] items =
        [
            Item(1, "Льон", 1, quantity: 12.5m, min: 30, price: 200), // 60 − 12.5 = 47.5 → 48
            Item(2, "Віскоза", 1, quantity: 0, min: 10, price: 150),  // 20
            Item(3, "Блискавка", 2, quantity: 5, min: 100, unit: "шт"),
            Item(4, "Пакет", null, quantity: 1, min: 10, unit: "шт"),
            Item(5, "Бірка", 2, quantity: 500, min: 100, unit: "шт"),  // вище мінімуму — не потрапляє
        ];

        var report = ReorderService.Build(items, Suppliers, new Dictionary<int, decimal>(), targetFactor: 2);

        Assert.Equal(["Текстиль-Опт", "Фурнітура", ReorderService.NoSupplierName], report.Suppliers.Select(s => s.SupplierName));
        var textile = report.Suppliers[0];
        Assert.Equal([48m, 20m], textile.Lines.OrderBy(l => l.ItemId).Select(l => l.Suggested));
        Assert.Equal(48 * 200 + 20 * 150, textile.Total);
        Assert.Equal("+380441234567", textile.Phone);
        Assert.Equal(report.Suppliers.Sum(s => s.Total), report.Total);
    }

    [Fact]
    public void Build_SubtractsAlreadyOrderedAndDropsFullyOrderedItems()
    {
        StockItemDto[] items = [Item(1, "Льон", 1, quantity: 10, min: 30), Item(2, "Віскоза", 1, quantity: 0, min: 10)];
        var onOrder = new Dictionary<int, decimal> { [1] = 15, [2] = 25 };

        var report = ReorderService.Build(items, Suppliers, onOrder, targetFactor: 2);

        var line = Assert.Single(Assert.Single(report.Suppliers).Lines);
        Assert.Equal((1, 15m, 35m), (line.ItemId, line.OnOrder, line.Suggested)); // 60 − 10 − 15; віскози замовлено вдосталь
    }

    [Fact]
    public void MessageText_ListsLinesForSupplier()
    {
        var text = ReorderService.MessageText([new ReorderLineDto(1, "Льон бежевий", "м", 0, 30, 0, 48, 0, 0)]);

        Assert.Contains("— Льон бежевий: 48 м", text);
    }
}
