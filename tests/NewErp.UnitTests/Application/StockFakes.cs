using NewErp.Application.Production;
using NewErp.Application.Stock;
using NewErp.Domain.Production;
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

/// <summary>Пошиті вироби на складах у пам'яті.</summary>
internal sealed class FakeSewnLocationRepository : ISewnLocationRepository
{
    public HashSet<int> WarehousesWithStock { get; } = [];

    public Task<IReadOnlyList<SewnBalance>> FindBalancesAsync(int productionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SewnBalance>>([]);

    public Task<IReadOnlyList<SewnBalanceRow>> ListBalancesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SewnBalanceRow>>([]);

    public Task<IReadOnlyList<ActiveSizeRow>> ListActiveSizesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ActiveSizeRow>>([]);

    public Task<IReadOnlyList<SewnTransfer>> ListTransfersAsync(int productionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SewnTransfer>>([]);

    public Task<bool> HoldsStockAsync(int warehouseId, CancellationToken ct) => Task.FromResult(WarehousesWithStock.Contains(warehouseId));

    public void Add(SewnBalance balance) { }

    public void Add(SewnTransfer transfer) { }
}
