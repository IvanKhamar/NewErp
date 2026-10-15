using NewErp.Domain.Common;
using NewErp.Domain.Production;

namespace NewErp.UnitTests.Domain;

public class ProductionModelTests
{
    private static ProductionModel NewModel(params string[] sizes)
    {
        var model = new ProductionModel("Сукня лляна", ProductionModel.Clothing, minTotal: 50, sortOrder: 0);
        model.SetSizes(sizes.Select(s => (s, 0m)).ToList());
        return model;
    }

    // Залишки змінюються лише операціями крою й пошиву; у тесті виставляємо їх напряму через рефлексію.
    private static void SetStock(ProductionSize size, string property, decimal value) =>
        typeof(ProductionSize).GetProperty(property)!.SetValue(size, value);

    [Fact]
    public void SetSizes_MatchesByNameKeepingObjectsAndStock()
    {
        var model = NewModel("S", "M", "L");
        var m = model.Sizes.Single(s => s.Name == "M");
        SetStock(m, nameof(ProductionSize.SewnStock), 7);

        var removed = model.SetSizes([("XS", 10m), ("m", 40m), ("L", 50m)]);

        Assert.Equal(["XS", "M", "L"], model.Sizes.OrderBy(s => s.SortOrder).Select(s => s.Name));
        Assert.Same(m, model.Sizes.Single(s => s.Name == "M")); // той самий розмір: id і залишок не губляться
        Assert.Equal((7m, 40m), (m.SewnStock, m.Percent));
        Assert.Single(removed); // прибрано S
    }

    [Fact]
    public void SetSizes_CannotRemoveSizeWithStock()
    {
        var model = NewModel("S", "M");
        SetStock(model.Sizes.Single(s => s.Name == "S"), nameof(ProductionSize.CutStock), 3);

        Assert.Throws<DomainException>(() => model.SetSizes([("M", 0m)]));
    }

    [Theory]
    [InlineData("S", "s")]
    [InlineData("S", " ")]
    public void SetSizes_RejectsDuplicatesAndEmptyNames(string first, string second) =>
        Assert.Throws<DomainException>(() => NewModel().SetSizes([(first, 0m), (second, 0m)]));

    [Fact]
    public void Update_RejectsUnknownType() =>
        Assert.Throws<DomainException>(() => new ProductionModel("Сукня", "bags", 0, 0));

    [Fact]
    public void SetLinks_ReplacesDirectAndCategoryLinksInPlace()
    {
        var model = NewModel("S");
        model.SetLinks([101, 102], [(5, "сукня", false), (6, null, false)]);
        var cat5 = model.CategoryLinks.Single(l => l.CategoryId == 5);

        model.SetLinks([102, 103], [(5, "Сукня лляна", true)]);

        Assert.Equal([102, 103], model.ProductLinks.Select(l => l.ProductId).Order());
        Assert.Same(cat5, Assert.Single(model.CategoryLinks));
        Assert.Equal(("Сукня лляна", true), (cat5.NameFilter, cat5.MatchExact));
    }

    [Fact]
    public void SetSewnBom_RowNeedsExactlyOneOfComponentOrItem()
    {
        var model = NewModel("S");

        Assert.Throws<DomainException>(() => model.SetSewnBom([(null, 1, 2, 1m)], componentIdsOfModel: [1]));
        Assert.Throws<DomainException>(() => model.SetSewnBom([(null, null, null, 1m)], componentIdsOfModel: [1]));
    }

    [Fact]
    public void SetSewnBom_RejectsForeignComponentAndNonPositiveQuantity()
    {
        var model = NewModel("S");

        Assert.Throws<DomainException>(() => model.SetSewnBom([(null, 99, null, 1m)], componentIdsOfModel: [1]));
        Assert.Throws<DomainException>(() => model.SetSewnBom([(null, null, 7, 0m)], componentIdsOfModel: [1]));
    }

    [Fact]
    public void SetSewnBom_ReplacesRows()
    {
        var model = NewModel("S");
        model.SetSewnBom([(null, 1, null, 1m), (null, null, 7, 0.5m)], componentIdsOfModel: [1]);

        model.SetSewnBom([(null, null, 8, 2m)], componentIdsOfModel: [1]);

        Assert.Equal(8, Assert.Single(model.SewnBom).ItemId);
    }

    [Fact]
    public void ComponentSetBom_RejectsDuplicatesAndForeignSize()
    {
        var component = new ProductionComponent(1, "Перед", 0);

        Assert.Throws<DomainException>(() => component.SetBom([(null, 7, 1m), (null, 7, 2m)], sizeIdsOfModel: [10]));
        Assert.Throws<DomainException>(() => component.SetBom([(99, 7, 1m)], sizeIdsOfModel: [10]));

        component.SetBom([(null, 7, 1.2m), (10, 7, 1.5m)], sizeIdsOfModel: [10]); // загальне правило + перекриття для розміру
        Assert.Equal(2, component.Bom.Count);
    }
}
