namespace NewErp.Domain.Production;

/// <summary>Запас розкроєних деталей одного розміру (dd_stock_production_component_stock).</summary>
public class ComponentStock
{
    private ComponentStock() { }

    public ComponentStock(int componentId, int sizeId)
    {
        ComponentId = componentId;
        SizeId = sizeId;
    }

    public int ComponentId { get; private set; }

    public int SizeId { get; private set; }

    public decimal Stock { get; internal set; }

    /// <summary>Розкроєно ще <paramref name="quantity"/> деталей.</summary>
    public void Add(decimal quantity) => Stock += quantity;

    /// <summary>Деталі пішли в пошив. Як і з розхідниками: якщо бракує, береться те, що є.</summary>
    /// <returns>Скільки справді взято.</returns>
    public decimal TakeUpTo(decimal wanted)
    {
        var taken = Math.Min(wanted, Math.Max(0, Stock));
        Stock -= taken;
        return taken;
    }

    /// <summary>Інвентаризація: запас стає порахованим. Повертає, чи він змінився.</summary>
    public bool Recount(decimal actual)
    {
        if (actual < 0 || decimal.Round(actual, 3) != actual)
            throw new Common.DomainException("Кількість деталей має бути від 0, не більше 3 знаків після коми.");
        var changed = actual != Stock;
        Stock = actual;
        return changed;
    }
}
