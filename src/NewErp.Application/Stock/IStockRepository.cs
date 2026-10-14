using NewErp.Application.Common;
using NewErp.Domain.Stock;

namespace NewErp.Application.Stock;

public interface IStockRepository
{
    Task<IReadOnlyList<LookupItem>> ListCategoriesAsync(CancellationToken ct);

    Task<IReadOnlyList<StockSupplierDto>> ListSuppliersAsync(CancellationToken ct);

    Task<IReadOnlyList<StockItemDto>> ListItemsAsync(StockItemFilter filter, CancellationToken ct);

    Task<StockItemDto?> GetItemDtoAsync(int itemId, CancellationToken ct);

    Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(int itemId, CancellationToken ct);

    Task<bool> CategoryExistsAsync(int categoryId, CancellationToken ct);

    Task<bool> CategoryNameTakenAsync(string name, CancellationToken ct);

    Task<bool> SupplierExistsAsync(int supplierId, CancellationToken ct);

    /// <summary>Завантажує позицію для зміни. Зміни зберігаються в <see cref="SaveChangesAsync"/>.</summary>
    Task<StockItem?> FindItemAsync(int itemId, CancellationToken ct);

    void Add(StockCategory category);

    void Add(StockSupplier supplier);

    void Add(StockItem item);

    void Add(StockMovement movement);

    Task SaveChangesAsync(CancellationToken ct);
}