using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Catalog;

/// <summary>Імпорт товарів із CSV над dd_product і dd_product_description (Python first, пункт 44).</summary>
public sealed class ProductImportStore(ErpDbContext db, IOptions<ErpOptions> options, TimeProvider clock) : IProductImportStore
{
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<IReadOnlyList<ImportedProduct>> FindAsync(IReadOnlyCollection<int> productIds, IReadOnlyCollection<string> models, CancellationToken ct)
    {
        if (productIds.Count == 0 && models.Count == 0)
            return [];
        var rows = await (
            from p in db.DdProducts.AsNoTracking()
            join d in db.DdProductDescriptions.Where(x => x.LanguageId == _languageId) on p.ProductId equals d.ProductId into ds
            from d in ds.DefaultIfEmpty()
            where productIds.Contains(p.ProductId) || models.Contains(p.Model)
            select new { p.ProductId, p.Model, p.Price, p.Weight, p.Length, p.Width, p.Height, p.SortOrder, p.Status, p.Sku, Name = d == null ? "" : d.Name })
            .ToListAsync(ct);
        return [.. rows.Select(r => new ImportedProduct(r.ProductId, r.Model, new Dictionary<string, object>
        {
            ["price"] = r.Price, ["weight"] = r.Weight, ["length"] = r.Length, ["width"] = r.Width, ["height"] = r.Height,
            ["sort_order"] = r.SortOrder, ["status"] = r.Status ? 1 : 0, ["sku"] = r.Sku, ["name"] = r.Name,
        }))];
    }

    public async Task UpdateAsync(int productId, IReadOnlyDictionary<string, object> values, CancellationToken ct)
    {
        // date_modified змінюється теж, тож форма товару, відкрита до імпорту, не перезапише його (409).
        var now = clock.GetLocalNow().DateTime;
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        decimal? D(string column) => values.TryGetValue(column, out var v) ? (decimal)v : null;
        int? I(string column) => values.TryGetValue(column, out var v) ? (int)v : null;
        string? S(string column) => values.TryGetValue(column, out var v) ? (string)v : null;
        var (price, weight, length, width, height) = (D("price"), D("weight"), D("length"), D("width"), D("height"));
        var (sortOrder, status, sku) = (I("sort_order"), I("status"), S("sku"));
        await db.DdProducts.Where(p => p.ProductId == productId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Price, p => price ?? p.Price)
            .SetProperty(p => p.Weight, p => weight ?? p.Weight)
            .SetProperty(p => p.Length, p => length ?? p.Length)
            .SetProperty(p => p.Width, p => width ?? p.Width)
            .SetProperty(p => p.Height, p => height ?? p.Height)
            .SetProperty(p => p.SortOrder, p => sortOrder ?? p.SortOrder)
            .SetProperty(p => p.Status, p => status == null ? p.Status : status == 1)
            .SetProperty(p => p.Sku, p => sku ?? p.Sku)
            .SetProperty(p => p.DateModified, now), ct);
        if (S("name") is { } name)
            await db.DdProductDescriptions.Where(d => d.ProductId == productId && d.LanguageId == _languageId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Name, name), ct);
    }
}
