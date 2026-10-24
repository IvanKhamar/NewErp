namespace NewErp.Domain.Production;

/// <summary>
/// Переміщення пошитих виробів розміру між складами (erp_production_transfer). null у From/To — основний склад.
/// Пошитий залишок не змінюється, тому в журнал виробництва переміщення не пишеться.
/// </summary>
public class SewnTransfer
{
    private SewnTransfer() { }

    internal SewnTransfer(int productionId, int sizeId, int? fromWarehouseId, int? toWarehouseId, decimal quantity, string comment, int userId, string username, DateTime createdAt)
    {
        ProductionId = productionId;
        SizeId = sizeId;
        FromWarehouseId = fromWarehouseId;
        ToWarehouseId = toWarehouseId;
        Quantity = quantity;
        Comment = comment;
        UserId = userId;
        Username = username;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }

    public int ProductionId { get; private set; }

    public int SizeId { get; private set; }

    public int? FromWarehouseId { get; private set; }

    public int? ToWarehouseId { get; private set; }

    public decimal Quantity { get; private set; }

    public string Comment { get; private set; } = "";

    public int UserId { get; private set; }

    public string Username { get; private set; } = "";

    public DateTime CreatedAt { get; private set; }
}
