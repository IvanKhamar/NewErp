using System.Globalization;

using Microsoft.EntityFrameworkCore;

using NewErp.Application.Reports;
using NewErp.Domain.Orders;
using NewErp.Domain.Reports;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Reports;

/// <summary>
/// Запити стандартних звітів OpenCart. Групи за періодом рахуємо в пам'яті: період не довший за рік, а межа дня, тижня й місяця
/// тоді одна на всі звіти (<see cref="SalesReport.PeriodStart"/>), без функцій дат MySQL у LINQ.
/// </summary>
public sealed class StandardReportStore(ErpDbContext db) : IStandardReportStore
{
    public Task<IReadOnlyList<IReadOnlyList<object?>>> RowsAsync(string kind, DateTime from, DateTime toExclusive, ReportGrouping grouping, int limit, CancellationToken ct) => kind switch
    {
        "sales-orders" => SalesOrdersAsync(from, toExclusive, grouping, limit, ct),
        "sales-returns" => SalesReturnsAsync(from, toExclusive, grouping, limit, ct),
        "sales-shipping" => SalesShippingAsync(from, toExclusive, grouping, limit, ct),
        "sales-coupons" => SalesCouponsAsync(from, toExclusive, limit, ct),
        "products-purchased" => ProductsPurchasedAsync(from, toExclusive, limit, ct),
        "customer-orders" => CustomerOrdersAsync(from, toExclusive, limit, ct),
        "customer-transactions" => CustomerTransactionsAsync(from, toExclusive, limit, ct),
        "customer-activity" => CustomerActivityAsync(from, toExclusive, limit, ct),
        "customer-searches" => CustomerSearchesAsync(from, toExclusive, limit, ct),
        "statistics" => StatisticsAsync(limit, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Невідомий звіт."),
    };

    private IQueryable<DdOrder> Placed(DateTime from, DateTime toExclusive) =>
        db.DdOrders.AsNoTracking().Where(o => o.OrderStatusId > 0 && o.OrderStatusId != CustomerRisk.CancelledStatusId && o.DateAdded >= @from && o.DateAdded < toExclusive);

    private static string Period(DateTime date, ReportGrouping grouping) =>
        SalesReport.PeriodStart(DateOnly.FromDateTime(date), grouping).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IReadOnlyList<IReadOnlyList<object?>> Rows(IEnumerable<object?[]> rows, int limit) => rows.Take(limit).ToList<IReadOnlyList<object?>>();

    private async Task<IReadOnlyList<IReadOnlyList<object?>>> SalesOrdersAsync(DateTime from, DateTime toExclusive, ReportGrouping grouping, int limit, CancellationToken ct)
    {
        var orders = await Placed(@from, toExclusive)
            .Select(o => new { o.DateAdded, o.Total, Products = db.DdOrderProducts.Where(p => p.OrderId == o.OrderId).Sum(p => (int?)p.Quantity) ?? 0 })
            .ToListAsync(ct);
        return Rows(orders.GroupBy(o => Period(o.DateAdded, grouping)).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new object?[] { g.Key, g.Count(), g.Sum(o => o.Products), g.Sum(o => o.Total) }), limit);
    }

    private async Task<IReadOnlyList<IReadOnlyList<object?>>> SalesReturnsAsync(DateTime from, DateTime toExclusive, ReportGrouping grouping, int limit, CancellationToken ct)
    {
        var dates = await db.DdReturns.AsNoTracking().Where(r => r.DateAdded >= @from && r.DateAdded < toExclusive).Select(r => r.DateAdded).ToListAsync(ct);
        return Rows(dates.GroupBy(d => Period(d, grouping)).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new object?[] { g.Key, g.Count() }), limit);
    }

    private async Task<IReadOnlyList<IReadOnlyList<object?>>> SalesShippingAsync(DateTime from, DateTime toExclusive, ReportGrouping grouping, int limit, CancellationToken ct)
    {
        var rows = await (
            from o in Placed(@from, toExclusive)
            join t in db.DdOrderTotals on o.OrderId equals t.OrderId
            where t.Code == "shipping"
            select new { o.DateAdded, t.Title, t.Value }).ToListAsync(ct);
        return Rows(rows.GroupBy(r => (Period: Period(r.DateAdded, grouping), r.Title))
            .OrderBy(g => g.Key.Period, StringComparer.Ordinal).ThenBy(g => g.Key.Title, StringComparer.Ordinal)
            .Select(g => new object?[] { g.Key.Period, g.Key.Title, g.Count(), g.Sum(r => r.Value) }), limit);
    }

    private async Task<IReadOnlyList<IReadOnlyList<object?>>> SalesCouponsAsync(DateTime from, DateTime toExclusive, int limit, CancellationToken ct)
    {
        var rows = await (
            from h in db.CouponHistories.AsNoTracking()
            join c in db.DdCoupons on h.CouponId equals c.CouponId
            where h.DateAdded >= @from && h.DateAdded < toExclusive
            select new { c.CouponId, c.Name, c.Code, h.OrderId, h.Amount }).ToListAsync(ct);
        return Rows(rows.GroupBy(r => (r.CouponId, r.Name, r.Code))
            .Select(g => new { g.Key.Name, g.Key.Code, Orders = g.Select(r => r.OrderId).Distinct().Count(), Discount = -g.Sum(r => r.Amount) })
            .OrderByDescending(r => r.Orders)
            .Select(r => new object?[] { r.Name, r.Code, r.Orders, r.Discount }), limit);
    }

    private async Task<IReadOnlyList<IReadOnlyList<object?>>> ProductsPurchasedAsync(DateTime from, DateTime toExclusive, int limit, CancellationToken ct)
    {
        var rows = await (
            from o in Placed(@from, toExclusive)
            join p in db.DdOrderProducts on o.OrderId equals p.OrderId
            group p by new { p.ProductId, p.Name, p.Model } into g
            orderby g.Sum(p => p.Quantity) descending
            select new { g.Key.Name, g.Key.Model, Quantity = g.Sum(p => p.Quantity), Total = g.Sum(p => p.Total) })
            .Take(limit)
            .ToListAsync(ct);
        return Rows(rows.Select(r => new object?[] { r.Name, r.Model, r.Quantity, r.Total }), limit);
    }

    private async Task<IReadOnlyList<IReadOnlyList<object?>>> CustomerOrdersAsync(DateTime from, DateTime toExclusive, int limit, CancellationToken ct)
    {
        var orders = await (
            from o in Placed(@from, toExclusive)
            join c in db.DdCustomers on o.CustomerId equals c.CustomerId
            where o.CustomerId > 0
            select new { o.CustomerId, c.Firstname, c.Lastname, c.Email, o.Total, Products = db.DdOrderProducts.Where(p => p.OrderId == o.OrderId).Sum(p => (int?)p.Quantity) ?? 0 })
            .ToListAsync(ct);
        return Rows(orders.GroupBy(o => (o.CustomerId, o.Firstname, o.Lastname, o.Email))
            .Select(g => new { Name = $"{g.Key.Firstname} {g.Key.Lastname}", g.Key.Email, Orders = g.Count(), Products = g.Sum(o => o.Products), Total = g.Sum(o => o.Total) })
            .OrderByDescending(r => r.Total)
            .Select(r => new object?[] { r.Name, r.Email, r.Orders, r.Products, r.Total }), limit);
    }

    private async Task<IReadOnlyList<IReadOnlyList<object?>>> CustomerTransactionsAsync(DateTime from, DateTime toExclusive, int limit, CancellationToken ct)
    {
        var rows = await (
            from t in db.CustomerTransactions.AsNoTracking()
            join c in db.DdCustomers on t.CustomerId equals c.CustomerId
            where t.DateAdded >= @from && t.DateAdded < toExclusive
            group t by new { t.CustomerId, c.Firstname, c.Lastname, c.Email } into g
            orderby g.Sum(t => t.Amount) descending
            select new { g.Key.Firstname, g.Key.Lastname, g.Key.Email, Amount = g.Sum(t => t.Amount) })
            .Take(limit)
            .ToListAsync(ct);
        return Rows(rows.Select(r => new object?[] { $"{r.Firstname} {r.Lastname}", r.Email, r.Amount }), limit);
    }

    private async Task<IReadOnlyList<IReadOnlyList<object?>>> CustomerActivityAsync(DateTime from, DateTime toExclusive, int limit, CancellationToken ct)
    {
        var rows = await (
            from a in db.CustomerActivities.AsNoTracking()
            join c in db.DdCustomers on a.CustomerId equals c.CustomerId into customers
            from c in customers.DefaultIfEmpty()
            where a.DateAdded >= @from && a.DateAdded < toExclusive
            orderby a.DateAdded descending
            select new { a.DateAdded, First = c == null ? null : c.Firstname, Last = c == null ? null : c.Lastname, a.Key, a.Ip })
            .Take(limit)
            .ToListAsync(ct);
        return Rows(rows.Select(r => new object?[]
        {
            r.DateAdded.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), r.First is null ? "гість" : $"{r.First} {r.Last}", r.Key, r.Ip,
        }), limit);
    }

    private async Task<IReadOnlyList<IReadOnlyList<object?>>> CustomerSearchesAsync(DateTime from, DateTime toExclusive, int limit, CancellationToken ct)
    {
        var rows = await db.CustomerSearches.AsNoTracking()
            .Where(s => s.DateAdded >= @from && s.DateAdded < toExclusive)
            .GroupBy(s => s.Keyword)
            .Select(g => new { Keyword = g.Key, Count = g.Count(), Products = g.Max(s => s.Products), Last = g.Max(s => s.DateAdded) })
            .OrderByDescending(r => r.Count)
            .Take(limit)
            .ToListAsync(ct);
        return Rows(rows.Select(r => new object?[] { r.Keyword, r.Count, r.Products, r.Last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }), limit);
    }

    private async Task<IReadOnlyList<IReadOnlyList<object?>>> StatisticsAsync(int limit, CancellationToken ct)
    {
        var rows = await db.StoreStatistics.AsNoTracking().OrderBy(s => s.Code).Take(limit).ToListAsync(ct);
        return Rows(rows.Select(s => new object?[] { s.Code, s.Value }), limit);
    }
}
