namespace NewErp.Domain.Production;

/// <summary>Рядок специфікації: розхідник і його кількість на одну одиницю виробу.</summary>
public class BomLine
{
    private BomLine() { }

    internal BomLine(int productId, int stockItemId, decimal quantity)
    {
        ProductId = productId;
        StockItemId = stockItemId;
        Quantity = quantity;
    }

    public int ProductId { get; private set; }

    public int StockItemId { get; private set; }

    public decimal Quantity { get; internal set; }
}
