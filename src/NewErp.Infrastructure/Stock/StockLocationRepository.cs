using Microsoft.EntityFrameworkCore;

using NewErp.Application.Stock;
using NewErp.Domain.Stock;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Stock;

public sealed class StockLocationRepository(ErpDbContext db) : IStockLocationRepository
{
    public async Task<IReadOnlyList<StockBalance>> FindBalancesAsync(int itemId, CancellationToken ct) =>
        await db.Set<StockBalance>().Where(b => b.ItemId == itemId).ToListAsync(ct);

    public async Task<IReadOnlyList<StockBalanceRow>> ListBalancesAsync(CancellationToken ct) =>
        await db.Set<StockBalance>().AsNoTracking()
            .Where(b => b.Quantity != 0)
            .Select(b => new StockBalanceRow(b.ItemId, b.WarehouseId, b.Quantity))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StockTransfer>> ListTransfersAsync(int itemId, CancellationToken ct) =>
        await db.Set<StockTransfer>().AsNoTracking()
            .Where(t => t.ItemId == itemId)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .ToListAsync(ct);

    public Task<bool> HoldsStockAsync(int warehouseId, CancellationToken ct) =>
        db.Set<StockBalance>().AnyAsync(b => b.WarehouseId == warehouseId && b.Quantity != 0, ct);

    public void Add(StockBalance balance) => db.Add(balance);

    public void Add(StockTransfer transfer) => db.Add(transfer);
}
