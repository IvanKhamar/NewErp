using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Catalog;

/// <summary>Товари магазину прямо з таблиць OpenCart, як у Java-версії (JdbcProductStore): той самий порядок і ті самі фільтри.</summary>
public sealed class ProductStore(ErpDbContext db, IOptions<ErpOptions> options) : IProductStore
{
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<PagedResult<ProductListItem>> ListAsync(ProductFilter filter, CancellationToken ct)
    {
        var products =
            from p in db.DdProducts.AsNoTracking()
            join d in db.DdProductDescriptions.Where(x => x.LanguageId == _languageId) on p.ProductId equals d.ProductId into ds
            from d in ds.DefaultIfEmpty()
            select new { p, Name = d == null ? "" : d.Name };

        if (filter.CategoryId is { } categoryId)
            products = products.Where(x => db.DdProductToCategories.Any(c => c.ProductId == x.p.ProductId && c.CategoryId == categoryId));
        if (filter.LabelId is { } labelId)
            products = products.Where(x => db.DdProductToLabels.Any(l => l.ProductId == x.p.ProductId && l.LabelId == labelId));
        if (filter.Enabled is { } enabled)
            products = products.Where(x => x.p.Status == enabled);
        if (!string.IsNullOrEmpty(filter.Search))
        {
            // Contains шукає буквально: "50%" знаходить саме "50%", а не все підряд.
            var term = filter.Search;
            products = int.TryParse(term, out var id)
                ? products.Where(x => x.p.ProductId == id || x.p.Model.Contains(term))
                : products.Where(x => x.Name.Contains(term) || x.p.Model.Contains(term));
        }

        var total = await products.CountAsync(ct);
        var items = await products
            .OrderBy(x => x.Name).ThenBy(x => x.p.ProductId)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => new ProductListItem(x.p.ProductId, x.Name, x.p.Model, x.p.Price, x.p.Status, x.p.Quantity, x.p.Image))
            .ToListAsync(ct);
        return new PagedResult<ProductListItem>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<ProductCardDto?> GetAsync(int productId, CancellationToken ct)
    {
        var product = await (
                from p in db.DdProducts.AsNoTracking()
                where p.ProductId == productId
                join d in db.DdProductDescriptions.Where(x => x.LanguageId == _languageId) on p.ProductId equals d.ProductId into ds
                from d in ds.DefaultIfEmpty()
                select new { p.ProductId, Name = d == null ? "" : d.Name, p.Model, p.Price, p.Status, p.Quantity, p.Image, p.DateModified })
            .SingleOrDefaultAsync(ct);
        if (product is null)
            return null;

        // Розмір без назви цією мовою все одно показуємо — його номером.
        var sizes = (await (
                from v in db.DdProductOptionValues.AsNoTracking()
                where v.ProductId == productId
                join n in db.DdOptionValueDescriptions.Where(x => x.LanguageId == _languageId) on v.OptionValueId equals n.OptionValueId into ns
                from n in ns.DefaultIfEmpty()
                orderby v.ProductOptionValueId
                select new { v.OptionValueId, Name = n == null ? null : n.Name, v.Quantity })
            .ToListAsync(ct))
            .Select(s => new ProductSize(s.OptionValueId, s.Name ?? $"#{s.OptionValueId}", s.Quantity))
            .ToList();
        var labels = await (
                from pl in db.DdProductToLabels.AsNoTracking()
                where pl.ProductId == productId
                join l in db.DdProductLabels on pl.LabelId equals l.LabelId
                orderby l.SortOrder, l.Name
                select new LookupItem(l.LabelId, l.Name))
            .ToListAsync(ct);
        var categories = (await (
                from pc in db.DdProductToCategories.AsNoTracking()
                where pc.ProductId == productId
                join d in db.DdCategoryDescriptions.Where(x => x.LanguageId == _languageId) on pc.CategoryId equals d.CategoryId into ds
                from d in ds.DefaultIfEmpty()
                select new { pc.CategoryId, Name = d == null ? null : d.Name })
            .ToListAsync(ct))
            .Select(c => new LookupItem(c.CategoryId, c.Name ?? $"#{c.CategoryId}"))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture)
            .ToList();
        return new ProductCardDto(product.ProductId, product.Name, product.Model, product.Price, product.Status, product.Quantity, product.Image,
            product.DateModified, sizes, labels, categories);
    }

    public async Task<IReadOnlyList<LookupItem>> LabelsAsync(CancellationToken ct) =>
        await db.DdProductLabels.AsNoTracking()
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
            .Select(l => new LookupItem(l.LabelId, l.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LookupItem>> CategoriesAsync(CancellationToken ct) =>
        await (
                from c in db.DdCategories.AsNoTracking()
                where c.Status
                join d in db.DdCategoryDescriptions.Where(x => x.LanguageId == _languageId) on c.CategoryId equals d.CategoryId
                orderby d.Name
                select new LookupItem(c.CategoryId, d.Name))
            .ToListAsync(ct);
}
