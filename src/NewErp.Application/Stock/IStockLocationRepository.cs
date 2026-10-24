using NewErp.Domain.Stock;

namespace NewErp.Application.Stock;

/// <summary>
/// Залишки розхідників на складах (erp_stock_location) і переміщення між ними. Зміни зберігає той самий
/// <see cref="IStockRepository.SaveChangesAsync"/>: обидва працюють з одним контекстом бази.
/// </summary>
public interface IStockLocationRepository
{
    /// <summary>Баланси позиції для зміни.</summary>
    Task<IReadOnlyList<StockBalance>> FindBalancesAsync(int itemId, CancellationToken ct);

    /// <summary>Усі ненульові баланси, лише для читання.</summary>
    Task<IReadOnlyList<StockBalanceRow>> ListBalancesAsync(CancellationToken ct);

    Task<IReadOnlyList<StockTransfer>> ListTransfersAsync(int itemId, CancellationToken ct);

    /// <summary>Чи лежить на складі хоч якийсь розхідник.</summary>
    Task<bool> HoldsStockAsync(int warehouseId, CancellationToken ct);

    void Add(StockBalance balance);

    void Add(StockTransfer transfer);
}
