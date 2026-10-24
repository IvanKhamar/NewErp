namespace NewErp.Domain.Stock;

/// <summary>
/// Залишок розхідника на окремому складі (наша таблиця erp_stock_location, склади — dd_warehouse).
/// Змінюється лише через <see cref="StockPlacement"/>.
/// </summary>
public class StockBalance
{
    private StockBalance() { }

    public StockBalance(int itemId, int warehouseId, decimal quantity = 0)
    {
        ItemId = itemId;
        WarehouseId = warehouseId;
        Quantity = quantity;
    }

    public int Id { get; private set; }

    public int ItemId { get; private set; }

    public int WarehouseId { get; private set; }

    public decimal Quantity { get; private set; }

    internal void Add(decimal delta) => Quantity += delta;
}
