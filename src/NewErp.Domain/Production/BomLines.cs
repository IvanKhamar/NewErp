namespace NewErp.Domain.Production;

/// <summary>
/// Рядок специфікації крою (dd_stock_production_component_bom): скільки розхідника йде на одну деталь.
/// <see cref="SizeId"/> = null — для всіх розмірів; рядок для конкретного розміру його перекриває.
/// </summary>
public class ComponentBomLine
{
    private ComponentBomLine() { }

    internal ComponentBomLine(int? sizeId, int itemId, decimal qtyPerUnit)
    {
        SizeId = sizeId;
        ItemId = itemId;
        QtyPerUnit = qtyPerUnit;
    }

    public int Id { get; private set; }

    public int ComponentId { get; private set; }

    public int? SizeId { get; private set; }

    public int ItemId { get; private set; }

    public decimal QtyPerUnit { get; private set; }
}

/// <summary>
/// Рядок специфікації пошиву (dd_stock_production_sewn_bom): на один пошитий виріб іде або деталь крою
/// (<see cref="ComponentId"/>), або розхідник напряму (<see cref="ItemId"/>: нитки, бірки).
/// Перекриття за розміром — як у <see cref="ComponentBomLine"/>.
/// </summary>
public class SewnBomLine
{
    private SewnBomLine() { }

    internal SewnBomLine(int? sizeId, int? componentId, int? itemId, decimal qtyPerUnit)
    {
        SizeId = sizeId;
        ComponentId = componentId;
        ItemId = itemId;
        QtyPerUnit = qtyPerUnit;
    }

    public int Id { get; private set; }

    public int ProductionId { get; private set; }

    public int? SizeId { get; private set; }

    public int? ComponentId { get; private set; }

    public int? ItemId { get; private set; }

    public decimal QtyPerUnit { get; private set; }
}
