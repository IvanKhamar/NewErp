namespace NewErp.Domain.Production;

/// <summary>Потреба одного замовлення в розмірі моделі: скільки замовлено і скільки пошитих воно вже тримає (списано зі складу).</summary>
public sealed record SewingDemand(int OrderId, int SizeId, decimal Ordered, decimal Held, DateOnly? Deadline)
{
    /// <summary>Скільки ще бракує цьому замовленню. Більше, ніж замовлено, не тримає, але про всяк випадок не йдемо в мінус.</summary>
    public decimal Missing => Math.Max(0, Ordered - Held);
}

/// <summary>
/// Рядок плану пошиву для розміру. Missing — скільки бракує активним замовленням, ToSew — скільки треба пошити після
/// вільного пошитого запасу, FromCut — скільки з цього вже розкроєно, ToCut — скільки ще треба розкроїти.
/// </summary>
public sealed record SewingPlanLine(
    int SizeId, decimal Ordered, decimal Missing, decimal ToSew, decimal FromCut, decimal ToCut, DateOnly? Deadline,
    IReadOnlyList<int> OrderIds);

/// <summary>
/// План пошиву: що пошити для активних замовлень. Старий сайт лише показував у списку замовлень дату готовності
/// (дата замовлення + термін виготовлення категорії), а скільки шити, рахували вручну.
/// </summary>
public static class SewingPlan
{
    /// <summary>
    /// Вільний пошитий запас (те, що замовлення вже списали, у ньому не лічиться) спершу покриває потребу, решту треба
    /// пошити. Розкроєне шиється першим, тож кроїти треба лише те, чого не покриває крій.
    /// Термін — найраніший серед замовлень, яким чогось бракує.
    /// </summary>
    public static SewingPlanLine ForSize(int sizeId, decimal sewnStock, decimal cutStock, IEnumerable<SewingDemand> demands)
    {
        var list = demands.Where(d => d.SizeId == sizeId).ToList();
        var missing = list.Sum(d => d.Missing);
        var toSew = Math.Max(0, missing - Math.Max(0, sewnStock));
        var fromCut = Math.Min(toSew, Math.Max(0, cutStock));
        var waiting = list.Where(d => d.Missing > 0).ToList();
        return new SewingPlanLine(
            sizeId,
            list.Sum(d => d.Ordered),
            missing,
            toSew,
            fromCut,
            toSew - fromCut,
            waiting.Min(d => d.Deadline),
            waiting.Select(d => d.OrderId).Distinct().Order().ToList());
    }

    /// <summary>
    /// Дата готовності, як у списку замовлень старого сайту: дата замовлення + термін виготовлення категорії.
    /// Термін 0 означає «не задано» (старий сайт тоді показував дату самого замовлення, і все виглядало простроченим).
    /// </summary>
    public static DateOnly? Deadline(DateTime orderDate, int productionDays) =>
        productionDays > 0 ? DateOnly.FromDateTime(orderDate).AddDays(productionDays) : null;
}
