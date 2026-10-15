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
}
