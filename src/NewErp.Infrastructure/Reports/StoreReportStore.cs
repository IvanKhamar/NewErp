using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Reports;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Reports;

public sealed class StoreReportStore(ErpDbContext db, IOptions<ErpOptions> options) : IStoreReportStore
{
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<IReadOnlyList<OnlineVisitorDto>> OnlineAsync(int limit, CancellationToken ct)
    {
        var rows = await (
            from v in db.CustomersOnline.AsNoTracking()
            join c in db.DdCustomers on v.CustomerId equals c.CustomerId into customers
            from c in customers.DefaultIfEmpty()
            orderby v.DateAdded descending
            select new { v.Ip, v.CustomerId, First = c == null ? null : c.Firstname, Last = c == null ? null : c.Lastname, v.Url, v.Referer, v.DateAdded })
            .Take(limit)
            .ToListAsync(ct);
        return rows.Select(r => new OnlineVisitorDto(r.Ip, r.CustomerId, r.First is null ? null : $"{r.First} {r.Last}", r.Url, r.Referer, r.DateAdded)).ToList();
    }

    public async Task<IReadOnlyList<ProductViewsRow>> ProductViewsAsync(int limit, CancellationToken ct) => await (
        from p in db.DdProducts.AsNoTracking()
        join d in db.DdProductDescriptions on new { p.ProductId, LanguageId = _languageId } equals new { d.ProductId, d.LanguageId }
        where p.Status && p.Viewed > 0
        orderby p.Viewed descending, p.ProductId
        select new ProductViewsRow(
            p.ProductId, d.Name, p.Model, p.Viewed,
            (from line in db.DdOrderProducts
             join o in db.DdOrders on line.OrderId equals o.OrderId
             where line.ProductId == p.ProductId && o.OrderStatusId > 0
             select (int?)line.Quantity).Sum() ?? 0))
        .Take(limit)
        .ToListAsync(ct);

    public async Task<IReadOnlyList<CustomerRewardDto>> RewardsAsync(int limit, CancellationToken ct)
    {
        var balances = await db.CustomerRewards.AsNoTracking()
            .GroupBy(r => r.CustomerId)
            .Select(g => new { CustomerId = g.Key, Points = g.Sum(r => r.Points), Last = g.Max(r => r.DateAdded) })
            .Where(b => b.Points != 0)
            .OrderByDescending(b => b.Points).ThenBy(b => b.CustomerId)
            .Take(limit)
            .ToListAsync(ct);
        var ids = balances.Select(b => b.CustomerId).ToList();
        var names = await db.DdCustomers.AsNoTracking()
            .Where(c => ids.Contains(c.CustomerId))
            .ToDictionaryAsync(c => c.CustomerId, c => $"{c.Firstname} {c.Lastname}", ct);
        return balances.Select(b => new CustomerRewardDto(b.CustomerId, names.GetValueOrDefault(b.CustomerId), b.Points, b.Last)).ToList();
    }

    public async Task<IReadOnlyList<TaxFact>> TaxesAsync(DateTime from, DateTime toExclusive, int? statusId, int excludedStatusId, CancellationToken ct) => await (
        from t in db.DdOrderTotals.AsNoTracking()
        join o in db.DdOrders on t.OrderId equals o.OrderId
        where t.Code == "tax" && o.DateAdded >= @from && o.DateAdded < toExclusive
            && (statusId != null ? o.OrderStatusId == statusId : o.OrderStatusId > 0 && o.OrderStatusId != excludedStatusId)
        select new TaxFact(t.Title, o.DateAdded, t.Value))
        .ToListAsync(ct);
}
