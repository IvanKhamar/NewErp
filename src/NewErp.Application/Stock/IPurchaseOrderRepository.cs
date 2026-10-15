using NewErp.Domain.Stock;

namespace NewErp.Application.Stock;

public interface IPurchaseOrderRepository
{
    Task<IReadOnlyList<PurchaseOrderSummaryDto>> ListAsync(PurchaseOrderFilter filter, CancellationToken ct);

    Task<PurchaseOrderDto?> GetDtoAsync(int id, CancellationToken ct);

    /// <summary>Замовлення з рядками для зміни.</summary>
    Task<PurchaseOrder?> FindAsync(int id, CancellationToken ct);

    /// <summary>Скільки кожного розхідника замовлено й ще не отримано (чернетки й надіслані замовлення).</summary>
    Task<IReadOnlyDictionary<int, decimal>> OnOrderAsync(CancellationToken ct);

    /// <summary>Активні розхідники для зміни залишку (приймання).</summary>
    Task<IReadOnlyList<StockItem>> FindItemsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct);

    void Add(PurchaseOrder order);

    void Add(StockMovement movement);

    Task SaveChangesAsync(CancellationToken ct);
}
