namespace NewErp.Domain.Production;

/// <summary>Скільки розхідника потрібно було на замовлення і скільки справді списано.</summary>
public class OrderWriteOffLine
{
    private OrderWriteOffLine() { }

    internal OrderWriteOffLine(int orderId, int stockItemId, decimal required, decimal writtenOff)
    {
        OrderId = orderId;
        StockItemId = stockItemId;
        Required = required;
        WrittenOff = writtenOff;
    }

    public int OrderId { get; private set; }

    public int StockItemId { get; private set; }

    public decimal Required { get; private set; }

    public decimal WrittenOff { get; private set; }

    /// <summary>Скільки не вистачило на складі в момент списання.</summary>
    public decimal Shortage => Required - WrittenOff;
}
