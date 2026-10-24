using NewErp.Domain.Common;
using NewErp.Domain.Production;
using NewErp.Domain.Stock;

namespace NewErp.UnitTests.Domain;

public class StocktakingTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 18, 0, 0);

    private static IReadOnlyList<string> Row(params string[] cells) => cells;

    [Fact]
    public void Parse_SkipsHeaderAndUncounted_ReadsUkrainianNumbers()
    {
        var parsed = Stocktaking.Parse([
            Row("ID", "Назва", "Категорія", "Од.", "За обліком", "Фактично"),
            Row("1", "Льон", "", "м", "10", "12,5"),
            Row("2", "Нитки", "", "шт", "1000", ""),
            Row("3", "Ґудзики", "", "шт", "0", "1 250"),
        ]);

        Assert.Empty(parsed.Errors);
        Assert.Equal([new(1, 12.5m), new(3, 1250m)], parsed.Counts);
    }

    [Fact]
    public void Parse_ReportsRowsAsExcelNumbersThem()
    {
        var parsed = Stocktaking.Parse([
            Row("ID", "", "", "", "", "Фактично"),
            Row("x", "", "", "", "", "1"),
            Row("4", "", "", "", "", "багато"),
            Row("5", "", "", "", "", "-1"),
            Row("6", "", "", "", "", "1,0001"),
            Row("7", "", "", "", "", "2"),
            Row("7", "", "", "", "", "3"),
        ]);

        Assert.Equal(
            ["Рядок 2: немає ID позиції або він не число.", "Рядок 3: «багато» — не кількість.",
             "Рядок 4: кількість має бути від 0, не більше 3 знаків після коми.",
             "Рядок 5: кількість має бути від 0, не більше 3 знаків після коми.", "Рядок 7: позиція 7 указана двічі."],
            parsed.Errors);
        Assert.Equal([new(7, 2m)], parsed.Counts);
    }

    [Fact]
    public void Comment_AddsNoteAndFitsLogColumn()
    {
        Assert.Equal("Інвентаризація", Stocktaking.Comment("  "));
        Assert.Equal("Інвентаризація: склад 2", Stocktaking.Comment(" склад 2 "));
        Assert.Equal(255, Stocktaking.Comment(new string('я', 300)).Length);
    }

    [Fact]
    public void RecountAt_Warehouse_ChangesThatPlaceAndTotal()
    {
        var item = new StockItem(1, null, "Льон", "м", 0);
        item.Adjust(30, "Прихід", 1, "sklad", Now);
        var placement = new StockPlacement(item, [new StockBalance(0, 7, 10)]);

        var movement = placement.RecountAt(7, 8, "Інвентаризація", 1, "sklad", Now);

        Assert.Equal((-2m, 28m, 8m, 20m), (movement!.Delta, item.Quantity, placement.At(7), placement.Main));
        Assert.Null(placement.RecountAt(null, 20, "Інвентаризація", 1, "sklad", Now));
        Assert.Throws<DomainException>(() => placement.RecountAt(null, -1, "Інвентаризація", 1, "sklad", Now));
    }

    [Fact]
    public void ModelRecount_CutAndSewnAtWarehouse()
    {
        var model = new ProductionModel("Сукня", ProductionModel.Clothing, 0, 0);
        model.SetSizes([("M", 100m)]);
        typeof(ProductionSize).GetProperty(nameof(ProductionSize.Id))!.SetValue(model.Sizes[0], 7);
        model.RecordCut(7, 5, null, 1, "sklad", Now);
        model.RecordSewn(7, 10, null, 1, "sklad", Now);
        var placement = new SewnPlacement(model, [new SewnBalance(7, 3, 4)]);

        var cut = model.Recount(7, ProductionLogEntry.Cut, 3, "Інвентаризація", 1, "sklad", Now);
        var sewn = placement.RecountAt(7, 3, 1, "Інвентаризація", 1, "sklad", Now);

        Assert.Equal((-2m, 3m), (cut!.StockAfter - cut.StockBefore, model.Sizes[0].CutStock));
        Assert.Equal((10m, 7m, 1m, 6m), (sewn!.StockBefore, model.Sizes[0].SewnStock, placement.At(7, 3), placement.At(7, null)));
        Assert.Null(model.Recount(7, ProductionLogEntry.Cut, 3, null, 1, "sklad", Now));
    }
}
