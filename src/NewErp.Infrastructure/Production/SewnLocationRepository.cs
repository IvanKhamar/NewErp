using Microsoft.EntityFrameworkCore;

using NewErp.Application.Production;
using NewErp.Domain.Production;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Production;

public sealed class SewnLocationRepository(ErpDbContext db) : ISewnLocationRepository
{
    public async Task<IReadOnlyList<SewnBalance>> FindBalancesAsync(int productionId, CancellationToken ct) =>
        await db.Set<SewnBalance>()
            .Where(b => db.ProductionSizes.Any(s => s.Id == b.SizeId && s.ProductionId == productionId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SewnBalanceRow>> ListBalancesAsync(CancellationToken ct) =>
        await db.Set<SewnBalance>().AsNoTracking()
            .Where(b => b.Quantity != 0)
            .Select(b => new SewnBalanceRow(b.SizeId, b.WarehouseId, b.Quantity))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ActiveSizeRow>> ListActiveSizesAsync(CancellationToken ct) =>
        await (from m in db.ProductionModels.AsNoTracking()
               join s in db.ProductionSizes.AsNoTracking() on m.Id equals s.ProductionId
               where m.IsActive
               orderby m.SortOrder, m.Name, s.SortOrder
               select new ActiveSizeRow(m.Id, m.Name, s.Id, s.Name, s.SewnStock))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SewnTransfer>> ListTransfersAsync(int productionId, CancellationToken ct) =>
        await db.Set<SewnTransfer>().AsNoTracking()
            .Where(t => t.ProductionId == productionId)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .ToListAsync(ct);

    public Task<bool> HoldsStockAsync(int warehouseId, CancellationToken ct) =>
        db.Set<SewnBalance>().AnyAsync(b => b.WarehouseId == warehouseId && b.Quantity != 0, ct);

    public void Add(SewnBalance balance) => db.Add(balance);

    public void Add(SewnTransfer transfer) => db.Add(transfer);
}
