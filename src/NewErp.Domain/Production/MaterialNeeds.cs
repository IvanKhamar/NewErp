namespace NewErp.Domain.Production;

/// <summary>Скільки розхідника треба на розмір і етап виробництва.</summary>
public sealed record MaterialNeed(int ItemId, string Stage, decimal Quantity)
{
    public const string Cutting = "Крій";
    public const string Sewing = "Пошив";
}

/// <summary>
/// Розхідники, які знадобляться, щоб розкроїти й пошити розмір моделі. Рахуються тими самими правилами, якими операції крою й
/// пошиву їх списують: крій кожної деталі бере розхідники за її специфікацією крою, пошив — прямі розхідники специфікації
/// пошиву (нитки, бірки); деталі для пошиву дає сам крій, тож окремо їх не рахуємо.
/// </summary>
public static class MaterialNeeds
{
    public static IEnumerable<MaterialNeed> ForSize(
        ProductionModel model, IEnumerable<ProductionComponent> components, int sizeId, decimal toCut, decimal toSew)
    {
        if (toCut > 0)
            foreach (var component in components)
                foreach (var line in BomResolver.Resolve(component.Bom, sizeId, b => b.SizeId, b => b.ItemId))
                    yield return new MaterialNeed(line.ItemId, MaterialNeed.Cutting, line.QtyPerUnit * toCut);

        if (toSew > 0)
            foreach (var line in BomResolver.Resolve(model.SewnBom, sizeId, b => b.SizeId, b => b.ItemId))
                yield return new MaterialNeed(line.ItemId!.Value, MaterialNeed.Sewing, line.QtyPerUnit * toSew);
    }
}
