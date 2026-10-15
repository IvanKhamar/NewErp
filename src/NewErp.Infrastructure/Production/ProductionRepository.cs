using Microsoft.EntityFrameworkCore;

using NewErp.Application.Production;
using NewErp.Domain.Production;
using NewErp.Domain.Stock;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Production;

public sealed class ProductionRepository(ErpDbContext db) : IProductionRepository
{
    public async Task<IReadOnlyList<BomSummaryDto>> ListAsync(CancellationToken ct) =>
        await db.Boms.AsNoTracking()
            .OrderBy(b => b.ProductId)
            .Select(b => new BomSummaryDto(b.ProductId, b.Lines.Count, b.UpdatedAt))
            .ToListAsync(ct);

    public async Task<BomDto?> GetDtoAsync(int productId, CancellationToken ct)
    {
        var bom = await db.Boms.AsNoTracking()
            .Where(b => b.ProductId == productId)
            .Select(b => new { b.UpdatedByUserId, b.UpdatedAt })
            .FirstOrDefaultAsync(ct);
        if (bom is null)
            return null;

        var lines = await (
            from l in db.BomLines.AsNoTracking()
            join i in db.StockItems on l.StockItemId equals i.Id
            where l.ProductId == productId
            orderby i.Name
            select new BomLineDto(l.StockItemId, i.Name, i.Unit, l.Quantity)
        ).ToListAsync(ct);

        return new BomDto(productId, lines, bom.UpdatedByUserId, bom.UpdatedAt);
    }

    public Task<Bom?> FindAsync(int productId, CancellationToken ct) =>
        db.Boms.Include(b => b.Lines).FirstOrDefaultAsync(b => b.ProductId == productId, ct);

    public async Task<IReadOnlyList<int>> MissingStockItemIdsAsync(IReadOnlyCollection<int> stockItemIds, CancellationToken ct)
    {
        var existing = await db.StockItems.Where(i => stockItemIds.Contains(i.Id)).Select(i => i.Id).ToListAsync(ct);
        return stockItemIds.Except(existing).ToList();
    }

    public async Task<IReadOnlyList<OrderProductLine>?> GetOrderProductsAsync(int orderId, CancellationToken ct)
    {
        if (!await db.DdOrders.AnyAsync(o => o.OrderId == orderId, ct))
            return null;

        return await db.DdOrderProducts.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .Select(p => new OrderProductLine(p.ProductId, p.Name, p.Quantity))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BomComponent>> GetComponentsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct) =>
        await (
            from l in db.BomLines.AsNoTracking()
            join i in db.StockItems on l.StockItemId equals i.Id
            where productIds.Contains(l.ProductId)
            select new BomComponent(l.ProductId, l.StockItemId, i.Name, i.Unit, l.Quantity, i.Quantity)
        ).ToListAsync(ct);

    public Task<bool> WriteOffExistsAsync(int orderId, CancellationToken ct) =>
        db.OrderWriteOffs.AnyAsync(w => w.OrderId == orderId, ct);

    public async Task<OrderWriteOffDto?> GetWriteOffDtoAsync(int orderId, CancellationToken ct)
    {
        var writeOff = await db.OrderWriteOffs.AsNoTracking()
            .Where(w => w.OrderId == orderId)
            .Select(w => new { w.UserId, w.CreatedAt })
            .FirstOrDefaultAsync(ct);
        if (writeOff is null)
            return null;

        var lines = await (
            from l in db.OrderWriteOffLines.AsNoTracking()
            join i in db.StockItems on l.StockItemId equals i.Id
            where l.OrderId == orderId
            orderby i.Name
            select new OrderWriteOffLineDto(l.StockItemId, i.Name, i.Unit, l.Required, l.WrittenOff, l.Required - l.WrittenOff)
        ).ToListAsync(ct);

        return new OrderWriteOffDto(orderId, writeOff.UserId, writeOff.CreatedAt, lines);
    }

    public async Task<IReadOnlyList<StockItem>> FindStockItemsAsync(IReadOnlyCollection<int> stockItemIds, CancellationToken ct) =>
        await db.StockItems.Where(i => stockItemIds.Contains(i.Id)).ToListAsync(ct);

    public void Add(Bom bom) => db.Boms.Add(bom);

    public void Remove(Bom bom) => db.Boms.Remove(bom);

    public void Add(OrderWriteOff writeOff) => db.OrderWriteOffs.Add(writeOff);

    public void Add(StockMovement movement) => db.StockMovements.Add(movement);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
