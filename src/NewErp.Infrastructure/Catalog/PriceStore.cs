using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Catalog;

/// <summary>Масові ціни над dd_product, dd_product_description і dd_product_special (Python first, пункт 46).</summary>
public sealed class PriceStore(ErpDbContext db, IOptions<ErpOptions> options, TimeProvider clock) : IPriceStore
{
    /// <summary>Роздрібна група покупців: саме її акції показує вітрина.</summary>
    private const int SaleGroupId = 1;

    private readonly int _languageId = options.Value.LanguageId;

    public async Task<IReadOnlyList<PricedProduct>> ProductsAsync(
        IReadOnlyCollection<int> categoryIds, IReadOnlyCollection<int> manufacturerIds, CancellationToken ct)
    {
        IQueryable<DdProduct> products = db.DdProducts.AsNoTracking();
        if (categoryIds.Count > 0)
            products = products.Where(p => db.DdProductToCategories.Any(c => c.ProductId == p.ProductId && categoryIds.Contains(c.CategoryId)));
        if (manufacturerIds.Count > 0)
            products = products.Where(p => manufacturerIds.Contains(p.ManufacturerId));
        var rows = await (
            from p in products
            join d in db.DdProductDescriptions.Where(x => x.LanguageId == _languageId) on p.ProductId equals d.ProductId into ds
            from d in ds.DefaultIfEmpty()
            orderby p.ProductId
            select new
            {
                p.ProductId, Name = d == null ? "" : d.Name, p.Model, p.Price,
                Percent = db.ProductSpecials.Where(s => s.ProductId == p.ProductId && s.CustomerGroupId == SaleGroupId).Max(s => (decimal?)s.Percent),
            }).ToListAsync(ct);
        return [.. rows.Select(r => new PricedProduct(r.ProductId, r.Name, r.Model, r.Price, r.Percent))];
    }

    public async Task SetPricesAsync(IReadOnlyDictionary<int, decimal> prices, CancellationToken ct)
    {
        // date_modified змінюється теж, тож форма товару, відкрита до зміни, не перезапише ціну (409).
        var now = clock.GetLocalNow().DateTime;
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        foreach (var (productId, price) in prices)
            await db.DdProducts.Where(p => p.ProductId == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, price).SetProperty(p => p.DateModified, now), ct);
    }

    public async Task SetSalePercentsAsync(IReadOnlyCollection<int> productIds, decimal percent, CancellationToken ct)
    {
        var withSale = await db.ProductSpecials
            .Where(s => productIds.Contains(s.ProductId) && s.CustomerGroupId == SaleGroupId)
            .Select(s => s.ProductId).Distinct().ToListAsync(ct);
        // Акція у відсотках має ціну 0: старий сайт рахує її від звичайної ціни.
        if (withSale.Count > 0)
            await db.ProductSpecials.Where(s => withSale.Contains(s.ProductId) && s.CustomerGroupId == SaleGroupId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Percent, percent).SetProperty(x => x.Price, 0m), ct);
        db.ProductSpecials.AddRange(productIds.Except(withSale).Select(id => new ProductSpecial
        {
            ProductId = id, CustomerGroupId = SaleGroupId, Priority = 1, Price = 0, Percent = percent,
            DateStart = ProductEditStore.NoStart, DateEnd = ProductEditStore.NoEnd,
        }));
        await db.SaveChangesAsync(ct);
    }

    public Task RemoveSalesAsync(IReadOnlyCollection<int> productIds, CancellationToken ct) =>
        db.ProductSpecials.Where(s => productIds.Contains(s.ProductId) && s.CustomerGroupId == SaleGroupId).ExecuteDeleteAsync(ct);
}

/// <summary>Фільтри розмірів над dd_product_option_value, dd_filter і dd_product_filter (Python first, пункт 52).</summary>
public sealed class SizeFilterStore(ErpDbContext db, IOptions<ErpOptions> options) : ISizeFilterStore
{
    /// <summary>«Розмір» серед фільтрів вітрини (стала старої консолі).</summary>
    private const int SizeFilterGroupId = 3;

    private readonly int _languageId = options.Value.LanguageId;

    public async Task<IReadOnlyList<(int ProductId, string Size)>> ProductSizesAsync(CancellationToken ct)
    {
        var rows = await (
            from v in db.DdProductOptionValues
            join n in db.DdOptionValueDescriptions on new { v.OptionValueId, LanguageId = _languageId } equals new { n.OptionValueId, n.LanguageId }
            join o in db.DdOptionDescriptions on new { v.OptionId, LanguageId = _languageId } equals new { o.OptionId, o.LanguageId }
            where o.Name.Contains(ProductEditStore.SizeOptionName)
            select new { v.ProductId, n.Name }).ToListAsync(ct);
        return [.. rows.Select(r => (r.ProductId, r.Name))];
    }

    public async Task<Dictionary<string, int>> SizeFiltersAsync(CancellationToken ct)
    {
        var rows = await db.DdFilterDescriptions
            .Where(d => d.FilterGroupId == SizeFilterGroupId && d.LanguageId == _languageId)
            .OrderBy(d => d.FilterId).Select(d => new { d.Name, d.FilterId }).ToListAsync(ct);
        var filters = new Dictionary<string, int>();
        foreach (var row in rows)
            filters[row.Name.Trim()] = row.FilterId; // as the Python version: a repeated name keeps the last filter
        return filters;
    }

    public async Task<int> CreateSizeFilterAsync(string name, CancellationToken ct)
    {
        var filter = new DdFilter { FilterGroupId = SizeFilterGroupId, SortOrder = 0 };
        db.DdFilters.Add(filter);
        await db.SaveChangesAsync(ct);
        db.DdFilterDescriptions.Add(new DdFilterDescription
        {
            FilterId = filter.FilterId, LanguageId = _languageId, FilterGroupId = SizeFilterGroupId, Name = name,
        });
        await db.SaveChangesAsync(ct);
        return filter.FilterId;
    }

    public async Task<HashSet<(int ProductId, int FilterId)>> SizeLinksAsync(CancellationToken ct)
    {
        var rows = await (
            from pf in db.DdProductFilters
            join f in db.DdFilters on pf.FilterId equals f.FilterId
            where f.FilterGroupId == SizeFilterGroupId
            select new { pf.ProductId, pf.FilterId }).ToListAsync(ct);
        return [.. rows.Select(r => (r.ProductId, r.FilterId))];
    }

    public async Task ChangeLinksAsync(
        IReadOnlyCollection<(int ProductId, int FilterId)> add, IReadOnlyCollection<(int ProductId, int FilterId)> remove, CancellationToken ct)
    {
        foreach (var (productId, filterId) in remove)
            await db.DdProductFilters.Where(x => x.ProductId == productId && x.FilterId == filterId).ExecuteDeleteAsync(ct);
        db.DdProductFilters.AddRange(add.Select(x => new DdProductFilter { ProductId = x.ProductId, FilterId = x.FilterId }));
        await db.SaveChangesAsync(ct);
    }
}
