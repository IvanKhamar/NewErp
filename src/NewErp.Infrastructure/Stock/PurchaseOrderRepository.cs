using Microsoft.EntityFrameworkCore;

using NewErp.Application.Stock;
using NewErp.Domain.Stock;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Stock;

public sealed class PurchaseOrderRepository(ErpDbContext db) : IPurchaseOrderRepository
{
    public async Task<IReadOnlyList<PurchaseOrderSummaryDto>> ListAsync(PurchaseOrderFilter filter, CancellationToken ct)
    {
        var orders = db.PurchaseOrders.AsNoTracking();
        if (!string.IsNullOrEmpty(filter.Status))
            orders = orders.Where(o => o.Status == filter.Status);

        return await (
            from o in orders
            join s in db.StockSuppliers on o.SupplierId equals s.Id into ss
            from s in ss.DefaultIfEmpty()
            orderby o.CreatedAt descending, o.Id descending
            select new PurchaseOrderSummaryDto(
                o.Id, o.SupplierId, s == null ? null : s.Name, o.Status, o.CreatedAt,
                o.Lines.Count, o.Lines.Sum(l => l.Quantity * l.Price))
        ).ToListAsync(ct);
    }

    public async Task<PurchaseOrderDto?> GetDtoAsync(int id, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return null;

        var supplier = await db.StockSuppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == order.SupplierId, ct);
        var itemIds = order.Lines.Select(l => l.ItemId).ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);

        var lines = order.Lines
            .Select(l => new PurchaseOrderLineDto(
                l.Id, l.ItemId, items.GetValueOrDefault(l.ItemId)?.Name, items.GetValueOrDefault(l.ItemId)?.Unit,
                l.Quantity, l.Price, l.Quantity * l.Price, l.ReceivedQuantity, l.Remaining))
            .OrderBy(l => l.ItemName)
            .ToList();
        var message = ReorderService.MessageText(lines.Select(l =>
            new ReorderLineDto(l.ItemId, l.ItemName ?? $"позиція {l.ItemId}", l.Unit ?? "", 0, 0, 0, l.Quantity, l.Price, l.Sum)));

        return new PurchaseOrderDto(
            order.Id, order.SupplierId, supplier?.Name, supplier?.Phone, order.Status, order.Comment,
            order.CreatedAt, order.OrderedAt, order.ClosedAt, lines, lines.Sum(l => l.Sum), message);
    }

    public Task<PurchaseOrder?> FindAsync(int id, CancellationToken ct) =>
        db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyDictionary<int, decimal>> OnOrderAsync(CancellationToken ct)
    {
        var lines = await (
            from o in db.PurchaseOrders
            where o.Status == PurchaseOrder.Draft || o.Status == PurchaseOrder.Ordered
            from l in o.Lines
            select new { l.ItemId, l.Quantity, l.ReceivedQuantity }
        ).ToListAsync(ct);

        return lines
            .GroupBy(l => l.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(l => Math.Max(0, l.Quantity - l.ReceivedQuantity)));
    }

    public async Task<IReadOnlyList<StockItem>> FindItemsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct) =>
        await db.StockItems.Where(i => i.IsActive && itemIds.Contains(i.Id)).ToListAsync(ct);

    public void Add(PurchaseOrder order) => db.PurchaseOrders.Add(order);

    public void Add(StockMovement movement) => db.StockMovements.Add(movement);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
