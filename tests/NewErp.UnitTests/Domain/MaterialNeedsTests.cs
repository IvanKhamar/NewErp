using NewErp.Domain.Production;

namespace NewErp.UnitTests.Domain;

public class MaterialNeedsTests
{
    private const int SizeS = 1;
    private const int SizeM = 2;
    private const int Fabric = 10;
    private const int Lining = 11;
    private const int Thread = 20;
    private const int Label = 21;

    // Id розмірів і деталей дає база; у тесті виставляємо їх напряму через рефлексію, як у ProductionModelTests.
    private static void SetId(object entity, int id) => entity.GetType().GetProperty("Id")!.SetValue(entity, id);

    private static (ProductionModel Model, ProductionComponent[] Components) Dress()
    {
        var model = new ProductionModel("Сукня лляна", ProductionModel.Clothing, minTotal: 0, sortOrder: 0);
        model.SetSizes([("S", 50m), ("M", 50m)]);
        SetId(model.Sizes[0], SizeS);
        SetId(model.Sizes[1], SizeM);

        var front = new ProductionComponent(model.Id, "Перед", 0);
        var back = new ProductionComponent(model.Id, "Спинка", 1);
        SetId(front, 1);
        SetId(back, 2);
        // Тканина на перед: 0.8 м на всі розміри, але для M — 1 м; підкладка лише для спинки.
        front.SetBom([(null, Fabric, 0.8m), (SizeM, Fabric, 1m)], [SizeS, SizeM]);
        back.SetBom([(null, Fabric, 0.7m), (null, Lining, 0.5m)], [SizeS, SizeM]);
        // Пошив: деталі (їх дає крій, окремо не рахуються), нитки й бірка.
        model.SetSewnBom([(null, 1, null, 1m), (null, 2, null, 1m), (null, null, Thread, 0.1m), (null, null, Label, 1m)], [1, 2]);
        return (model, [front, back]);
    }

    [Fact]
    public void Cutting_TakesEveryComponentsBom_ForThatSize_AndSewing_OnlyDirectItems()
    {
        var (model, components) = Dress();

        var needs = MaterialNeeds.ForSize(model, components, SizeM, toCut: 2, toSew: 3).ToList();

        Assert.Equal(
            [
                new MaterialNeed(Fabric, MaterialNeed.Cutting, 2m),   // перед M: 1 м × 2
                new MaterialNeed(Fabric, MaterialNeed.Cutting, 1.4m), // спинка: 0.7 м × 2
                new MaterialNeed(Lining, MaterialNeed.Cutting, 1m),
                new MaterialNeed(Thread, MaterialNeed.Sewing, 0.3m),
                new MaterialNeed(Label, MaterialNeed.Sewing, 3m),
            ],
            needs);
    }

    [Fact]
    public void GeneralRow_AppliesToOtherSizes_AndNothingIsNeededForZero()
    {
        var (model, components) = Dress();

        Assert.Equal(1.6m, MaterialNeeds.ForSize(model, components, SizeS, toCut: 2, toSew: 0).First().Quantity); // 0.8 м × 2
        Assert.All(MaterialNeeds.ForSize(model, components, SizeS, toCut: 0, toSew: 2), n => Assert.Equal(MaterialNeed.Sewing, n.Stage));
        Assert.Empty(MaterialNeeds.ForSize(model, components, SizeS, toCut: 0, toSew: 0));
    }
}
