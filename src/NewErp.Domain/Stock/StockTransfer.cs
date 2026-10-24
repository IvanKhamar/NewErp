namespace NewErp.Domain.Stock;

/// <summary>
/// Переміщення розхідника між складами (erp_stock_transfer). null у From/To — основний склад.
/// Загальний залишок не змінюється, тому в журнал руху dd_stock_log переміщення не пишеться.
/// </summary>
public class StockTransfer
{
    private StockTransfer() { }

    internal StockTransfer(int itemId, int? fromWarehouseId, int? toWarehouseId, decimal quantity, string comment, int userId, string username, DateTime createdAt)
    {
        ItemId = itemId;
        FromWarehouseId = fromWarehouseId;
        ToWarehouseId = toWarehouseId;
        Quantity = quantity;
        Comment = comment;
        UserId = userId;
        Username = username;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }

    public int ItemId { get; private set; }

    public int? FromWarehouseId { get; private set; }

    public int? ToWarehouseId { get; private set; }

    public decimal Quantity { get; private set; }

    public string Comment { get; private set; } = "";

    public int UserId { get; private set; }

    public string Username { get; private set; } = "";

    public DateTime CreatedAt { get; private set; }
}
