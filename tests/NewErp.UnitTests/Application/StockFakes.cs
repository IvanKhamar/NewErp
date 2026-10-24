using NewErp.Application.Stock;
using NewErp.Domain.Stock;

namespace NewErp.UnitTests.Application;

/// <summary>Залишки на складах у пам'яті: за замовчуванням ніде нічого немає.</summary>
internal sealed class FakeStockLocationRepository : IStockLocationRepository
{
    public List<StockBalance> Balances { get; } = [];

    public HashSet<int> WarehousesWithStock { get; } = [];

    public Task<IReadOnlyList<StockBalance>> FindBalancesAsync(int itemId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockBalance>>(Balances.Where(b => b.ItemId == itemId).ToList());

    public Task<IReadOnlyList<StockBalanceRow>> ListBalancesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockBalanceRow>>(Balances.Select(b => new StockBalanceRow(b.ItemId, b.WarehouseId, b.Quantity)).ToList());

    public Task<IReadOnlyList<StockTransfer>> ListTransfersAsync(int itemId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockTransfer>>([]);

    public Task<bool> HoldsStockAsync(int warehouseId, CancellationToken ct) => Task.FromResult(WarehousesWithStock.Contains(warehouseId));

    public void Add(StockBalance balance) => Balances.Add(balance);

    public void Add(StockTransfer transfer) { }
}
