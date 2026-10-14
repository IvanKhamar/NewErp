using Microsoft.EntityFrameworkCore;

using NewErp.Application.Production;
using NewErp.Domain.Production;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Production;

public sealed class BomRepository(ErpDbContext db) : IBomRepository
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

    public void Add(Bom bom) => db.Boms.Add(bom);

    public void Remove(Bom bom) => db.Boms.Remove(bom);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
