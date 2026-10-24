namespace NewErp.Domain.Production;

/// <summary>Пошиті вироби розміру на окремому складі (наша таблиця erp_production_location). Змінюється лише через <see cref="SewnPlacement"/>.</summary>
public class SewnBalance
{
    private SewnBalance() { }

    public SewnBalance(int sizeId, int warehouseId, decimal quantity = 0)
    {
        SizeId = sizeId;
        WarehouseId = warehouseId;
        Quantity = quantity;
    }

    public int Id { get; private set; }

    public int SizeId { get; private set; }

    public int WarehouseId { get; private set; }

    public decimal Quantity { get; private set; }

    internal void Add(decimal delta) => Quantity += delta;
}
