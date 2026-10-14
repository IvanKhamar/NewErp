namespace NewErp.Domain.Stock;

/// <summary>Запис журналу руху розхідника. Створюється лише через <see cref="StockItem.Adjust"/>.</summary>
public class StockMovement
{
    private StockMovement() { }

    internal StockMovement(StockItem item, decimal delta, string reason, int userId, DateTime createdAt)
    {
        Item = item;
        Delta = delta;
        QuantityAfter = item.Quantity;
        Reason = reason;
        UserId = userId;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public int ItemId { get; private set; }

    public StockItem Item { get; private set; } = null!;

    public decimal Delta { get; private set; }

    public decimal QuantityAfter { get; private set; }

    public string Reason { get; private set; } = "";

    public int UserId { get; private set; }

    public DateTime CreatedAt { get; private set; }
}