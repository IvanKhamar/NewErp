using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Common;
using NewErp.Application.Marketing;
using NewErp.Domain.Marketing;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Marketing;

/// <summary>
/// Старий купон може мати дату 0000-00-00, яку DateTime не прочитає. Тому дати читаємо лише через CASE (нуль стає null),
/// а змінюємо командою UPDATE, не завантажуючи рядок.
/// </summary>
public sealed class CouponStore(ErpDbContext db, IOptions<ErpOptions> options) : ICouponStore
{
    private static readonly DateTime FirstValidDate = new(1000, 1, 1);

    private readonly int _languageId = options.Value.LanguageId;

    public async Task<IReadOnlyList<CouponListItem>> ListAsync(string search, int limit, CancellationToken ct)
    {
        var query = db.DdCoupons.AsNoTracking();
        if (search != "")
            query = query.Where(c => c.Code.Contains(search) || c.Name.Contains(search));
        var rows = await query
            .OrderByDescending(c => c.CouponId)
            .Take(limit)
            .Select(c => new
            {
                c.CouponId, c.Name, c.Code, c.Type, c.Discount, c.Total, c.UsesTotal, c.Status,
                Start = c.DateStart < FirstValidDate ? (DateTime?)null : c.DateStart,
                End = c.DateEnd < FirstValidDate ? (DateTime?)null : c.DateEnd,
                Used = db.CouponHistories.Count(h => h.CouponId == c.CouponId),
            })
            .ToListAsync(ct);
        return rows.Select(c => new CouponListItem(
            c.CouponId, c.Name, c.Code, c.Type, c.Discount, c.Total, Day(c.Start), Day(c.End), c.UsesTotal, c.Status, c.Used)).ToList();
    }

    public async Task<CouponCardDto?> GetAsync(int id, CancellationToken ct)
    {
        var c = await db.DdCoupons.AsNoTracking()
            .Where(x => x.CouponId == id)
            .Select(x => new
            {
                x.CouponId, x.Name, x.Code, x.Type, x.Discount, x.Total, x.Logged, x.Shipping, x.UsesTotal, x.UsesCustomer, x.Status, x.DateAdded,
                Start = x.DateStart < FirstValidDate ? (DateTime?)null : x.DateStart,
                End = x.DateEnd < FirstValidDate ? (DateTime?)null : x.DateEnd,
            })
            .FirstOrDefaultAsync(ct);
        if (c is null)
            return null;

        var history = await db.CouponHistories.AsNoTracking()
            .Where(h => h.CouponId == id)
            .OrderByDescending(h => h.DateAdded)
            .Select(h => new CouponHistoryRow(h.OrderId, h.CustomerId, h.Amount, h.DateAdded))
            .ToListAsync(ct);
        // uses_customer в OpenCart — varchar(11).
        var usesCustomer = int.TryParse(c.UsesCustomer, NumberStyles.None, CultureInfo.InvariantCulture, out var uses) ? uses : 0;
        return new CouponCardDto(
            c.CouponId, c.Name, c.Code, c.Type, c.Discount, c.Total, c.Logged, c.Shipping, Day(c.Start), Day(c.End), c.UsesTotal, usesCustomer, c.Status, c.DateAdded,
            await ProductsAsync(db.CouponProducts.Where(x => x.CouponId == id).Select(x => x.ProductId), ct),
            await CategoriesAsync(db.CouponCategories.Where(x => x.CouponId == id).Select(x => x.CategoryId), ct),
            await ProductsAsync(db.CouponProductExcludes.Where(x => x.CouponId == id).Select(x => x.ProductId), ct),
            await CategoriesAsync(db.CouponCategoryExcludes.Where(x => x.CouponId == id).Select(x => x.CategoryId), ct),
            history);
    }

    public Task<bool> CodeTakenAsync(string code, int? exceptId, CancellationToken ct) =>
        db.DdCoupons.AnyAsync(c => c.Code == code && (exceptId == null || c.CouponId != exceptId), ct);

    public async Task<IReadOnlyList<int>> MissingProductsAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var found = await db.DdProducts.Where(p => ids.Contains(p.ProductId)).Select(p => p.ProductId).ToListAsync(ct);
        return ids.Distinct().Except(found).ToList();
    }

    public async Task<IReadOnlyList<int>> MissingCategoriesAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var found = await db.DdCategoryDescriptions.Where(d => ids.Contains(d.CategoryId)).Select(d => d.CategoryId).ToListAsync(ct);
        return ids.Distinct().Except(found).ToList();
    }

    public async Task<int> AddAsync(Coupon coupon, DateTime now, CancellationToken ct)
    {
        var row = new DdCoupon
        {
            Name = coupon.Name, Code = coupon.Code, Type = coupon.Type, Discount = coupon.Discount, Total = coupon.Total,
            Logged = coupon.Logged, Shipping = coupon.Shipping, DateStart = coupon.Start.ToDateTime(TimeOnly.MinValue),
            DateEnd = coupon.End.ToDateTime(TimeOnly.MinValue), UsesTotal = coupon.UsesTotal,
            UsesCustomer = coupon.UsesCustomer.ToString(CultureInfo.InvariantCulture), Status = coupon.Active, DateAdded = now,
        };
        db.DdCoupons.Add(row);
        await db.SaveChangesAsync(ct);
        await AddListsAsync(row.CouponId, coupon, ct);
        return row.CouponId;
    }

    public async Task<bool> UpdateAsync(int id, Coupon coupon, CancellationToken ct)
    {
        var (start, end) = (coupon.Start.ToDateTime(TimeOnly.MinValue), coupon.End.ToDateTime(TimeOnly.MinValue));
        var usesCustomer = coupon.UsesCustomer.ToString(CultureInfo.InvariantCulture);
        if (!await db.DdCoupons.AnyAsync(c => c.CouponId == id, ct))
            return false;
        await db.DdCoupons.Where(c => c.CouponId == id).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.Name, coupon.Name)
            .SetProperty(c => c.Code, coupon.Code)
            .SetProperty(c => c.Type, coupon.Type)
            .SetProperty(c => c.Discount, coupon.Discount)
            .SetProperty(c => c.Total, coupon.Total)
            .SetProperty(c => c.Logged, coupon.Logged)
            .SetProperty(c => c.Shipping, coupon.Shipping)
            .SetProperty(c => c.DateStart, start)
            .SetProperty(c => c.DateEnd, end)
            .SetProperty(c => c.UsesTotal, coupon.UsesTotal)
            .SetProperty(c => c.UsesCustomer, usesCustomer)
            .SetProperty(c => c.Status, coupon.Active), ct);
        await DeleteListsAsync(id, ct);
        await AddListsAsync(id, coupon, ct);
        return true;
    }

    public Task<int> UsedAsync(int id, CancellationToken ct) => db.CouponHistories.CountAsync(h => h.CouponId == id, ct);

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await DeleteListsAsync(id, ct);
        await db.DdCoupons.Where(c => c.CouponId == id).ExecuteDeleteAsync(ct);
    }

    private async Task AddListsAsync(int id, Coupon coupon, CancellationToken ct)
    {
        db.CouponProducts.AddRange(coupon.ProductIds.Select(p => new CouponProduct { CouponId = id, ProductId = p }));
        db.CouponCategories.AddRange(coupon.CategoryIds.Select(c => new CouponCategory { CouponId = id, CategoryId = c }));
        db.CouponProductExcludes.AddRange(coupon.ExcludedProductIds.Select(p => new CouponProductExclude { CouponId = id, ProductId = p }));
        db.CouponCategoryExcludes.AddRange(coupon.ExcludedCategoryIds.Select(c => new CouponCategoryExclude { CouponId = id, CategoryId = c }));
        await db.SaveChangesAsync(ct);
    }

    private async Task DeleteListsAsync(int id, CancellationToken ct)
    {
        await db.CouponProducts.Where(x => x.CouponId == id).ExecuteDeleteAsync(ct);
        await db.CouponCategories.Where(x => x.CouponId == id).ExecuteDeleteAsync(ct);
        await db.CouponProductExcludes.Where(x => x.CouponId == id).ExecuteDeleteAsync(ct);
        await db.CouponCategoryExcludes.Where(x => x.CouponId == id).ExecuteDeleteAsync(ct);
    }

    private async Task<IReadOnlyList<LookupItem>> ProductsAsync(IQueryable<int> ids, CancellationToken ct) => await (
        from id in ids
        join d in db.DdProductDescriptions on new { ProductId = id, LanguageId = _languageId } equals new { d.ProductId, d.LanguageId } into names
        from d in names.DefaultIfEmpty()
        orderby id
        select new LookupItem(id, d == null ? "" : d.Name)).ToListAsync(ct);

    private async Task<IReadOnlyList<LookupItem>> CategoriesAsync(IQueryable<int> ids, CancellationToken ct) => await (
        from id in ids
        join d in db.DdCategoryDescriptions on new { CategoryId = id, LanguageId = _languageId } equals new { d.CategoryId, d.LanguageId } into names
        from d in names.DefaultIfEmpty()
        orderby id
        select new LookupItem(id, d == null ? "" : d.Name)).ToListAsync(ct);

    private static DateOnly? Day(DateTime? value) => value is { } day ? DateOnly.FromDateTime(day) : null;
}
