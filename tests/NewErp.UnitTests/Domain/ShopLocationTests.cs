using NewErp.Domain.Common;
using NewErp.Domain.Locations;

namespace NewErp.UnitTests.Domain;

public class ShopLocationTests
{
    private static IReadOnlyList<string> Row(params string[] cells) => cells;

    [Fact]
    public void Validate_TrimsFields_NameRequiredUpTo64()
    {
        var data = ShopLocation.Validate(" Склад на Подолі ", null, "Оксана", "067 123 45 67", "Київ", "вул. Сагайдачного, 1", "склад");
        Assert.Equal(("Склад на Подолі", ""), (data.Name, data.Description));
        Assert.Throws<DomainException>(() => ShopLocation.Validate(" ", null, null, null, null, null, null));
        Assert.Throws<DomainException>(() => ShopLocation.Validate(new string('я', 65), null, null, null, null, null, null));
    }

    [Fact]
    public void ParseImport_SkipsHeaderAndEmptyRows_IdOptional()
    {
        var (rows, errors) = ShopLocation.ParseImport([
            Row("ID", "Назва"), Row("5", "Склад", "", "", "", "Київ"), Row("", "", "", ""), Row("", "Пункт видачі", "", "", "", "Львів", "", "пункт"),
        ]);

        Assert.Empty(errors);
        Assert.Equal([(2, 5, "Склад"), (4, 0, "Пункт видачі")], rows.Select(r => (r.RowNumber, r.Id, r.Data.Name)));
    }

    [Fact]
    public void ParseImport_CollectsAllErrors_WithRowNumbers()
    {
        var (_, errors) = ShopLocation.ParseImport([
            Row("ID", "Назва"), Row("abc", "Склад"), Row("", ""+"", "опис"), Row("7", "A"), Row("7", "B"),
        ]);

        Assert.Equal(3, errors.Count);
        Assert.Contains("Рядок 2", errors[0]);
        Assert.Contains("Рядок 3", errors[1]);
        Assert.Contains("ID 7 повторюється в рядках 4, 5", errors[2]);
        Assert.Single(ShopLocation.ParseImport([Row("ID", "Назва")]).Errors);
    }
}
