using Microsoft.EntityFrameworkCore;

using NewErp.Application.Reports;
using NewErp.Domain.Reports;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Reports;

public sealed class ReportStore(ErpDbContext db) : IReportStore
{
    public async Task<IReadOnlyList<SaleFact>> SalesAsync(DateTime from, DateTime toExclusive, int? statusId, int excludedStatusId, CancellationToken ct)
    {
        var orders = Orders(from, toExclusive, statusId, excludedStatusId);
        // Кількість одиниць товару рахуємо окремим запитом із групуванням, а не підзапитом для кожного замовлення.
        var products = await (
                from p in db.DdOrderProducts
                join o in orders on p.OrderId equals o.OrderId
                group p by p.OrderId into g
                select new { OrderId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.OrderId, x => x.Quantity, ct);
        var rows = await orders.Select(o => new { o.OrderId, o.DateAdded, o.Total }).ToListAsync(ct);
        return rows.Select(o => new SaleFact(o.DateAdded, o.Total, products.GetValueOrDefault(o.OrderId))).ToList();
    }

    public async Task<IReadOnlyList<ProductSalesRow>> ProductsAsync(
        DateTime from, DateTime toExclusive, int? statusId, int excludedStatusId, int limit, CancellationToken ct)
    {
        var orders = Orders(from, toExclusive, statusId, excludedStatusId);
        var rows = await (
                from p in db.DdOrderProducts
                join o in orders on p.OrderId equals o.OrderId
                group p by p.ProductId into g
                select new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity), Total = g.Sum(x => x.Total) })
            .OrderByDescending(x => x.Quantity).ThenByDescending(x => x.Total).ThenBy(x => x.ProductId)
            .Take(limit)
            .ToListAsync(ct);

        // Назва й модель — з найновішого рядка замовлення цього товару (товар могли перейменувати).
        var ids = rows.Select(r => r.ProductId).ToList();
        var names = (await db.DdOrderProducts.AsNoTracking()
                .Where(p => ids.Contains(p.ProductId))
                .OrderByDescending(p => p.OrderProductId)
                .Select(p => new { p.ProductId, p.Name, p.Model })
                .ToListAsync(ct))
            .DistinctBy(p => p.ProductId)
            .ToDictionary(p => p.ProductId);
        return rows.Select(r => new ProductSalesRow(
                r.ProductId, names.GetValueOrDefault(r.ProductId)?.Name ?? "", names.GetValueOrDefault(r.ProductId)?.Model ?? "", r.Quantity, r.Total))
            .ToList();
    }

    public Task<int> NewCustomersAsync(DateTime from, DateTime toExclusive, CancellationToken ct) =>
        db.DdCustomers.CountAsync(c => c.DateAdded >= from && c.DateAdded < toExclusive, ct);

    private IQueryable<DdOrder> Orders(DateTime from, DateTime toExclusive, int? statusId, int excludedStatusId)
    {
        var orders = db.DdOrders.AsNoTracking().Where(o => o.DateAdded >= from && o.DateAdded < toExclusive);
        return statusId is { } id
            ? orders.Where(o => o.OrderStatusId == id)
            : orders.Where(o => o.OrderStatusId > 0 && o.OrderStatusId != excludedStatusId);
    }
}
