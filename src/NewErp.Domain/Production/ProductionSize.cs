namespace NewErp.Domain.Production;

/// <summary>Розмір моделі з власними залишками крою й пошиву (dd_stock_production_sizes).</summary>
public class ProductionSize
{
    private ProductionSize() { }

    internal ProductionSize(string name, decimal percent, int sortOrder)
    {
        Name = name;
        Percent = percent;
        SortOrder = sortOrder;
    }

    public int Id { get; private set; }

    public int ProductionId { get; private set; }

    public string Name { get; private set; } = "";

    /// <summary>Частка розміру в партії, %.</summary>
    public decimal Percent { get; internal set; }

    /// <summary>Розкроєно, але ще не пошито.</summary>
    public decimal CutStock { get; internal set; }

    /// <summary>Пошито й готово до продажу.</summary>
    public decimal SewnStock { get; internal set; }

    public int SortOrder { get; internal set; }

    /// <summary>Значення опції OpenCart (розмір у картці товару), з яким зв'язано цей розмір.</summary>
    public int? OptionValueId { get; internal set; }
}
