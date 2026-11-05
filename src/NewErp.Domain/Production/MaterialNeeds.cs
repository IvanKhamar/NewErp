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
    /// <summary>
    /// Штуки на розмір, щоб модель досягла мінімуму: загальна кількість за частками розмірів у партії, округлена вгору до цілих
    /// (краще трохи більше, ніж замало). Без часток розміри отримують порівну.
    /// </summary>
    public static IReadOnlyDictionary<int, decimal> SpreadByShare(decimal total, IReadOnlyDictionary<int, decimal> shares)
    {
        if (total <= 0 || shares.Count == 0)
            return new Dictionary<int, decimal>();
        var weights = shares.Where(s => s.Value > 0).ToDictionary(s => s.Key, s => s.Value);
        if (weights.Count == 0)
            weights = shares.ToDictionary(s => s.Key, _ => 1m);
        var whole = weights.Values.Sum();
        return weights.ToDictionary(w => w.Key, w => Math.Ceiling(total * w.Value / whole));
    }

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
