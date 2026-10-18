using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Partners;
using NewErp.Domain.Partners;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Partners;

public sealed class PartnerRepository(ErpDbContext db, IOptions<ErpOptions> options) : IPartnerRepository
{
    // Розмірами старий модуль вважає опції, у назві яких є «Розмір».
    private const string SizeOptionName = "Розмір";

    private readonly int _languageId = options.Value.LanguageId;

    public async Task<IReadOnlyList<PartnerListItemDto>> ListAsync(CancellationToken ct) =>
        await db.Partners.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PartnerListItemDto(p.Id, p.Code, p.Name, p.VendorName, p.FeedEnabled, p.IsActive,
                p.CategoryMap.Count, p.ProductMap.Count))
            .ToListAsync(ct);

    public async Task<PartnerDto?> GetDtoAsync(int id, CancellationToken ct)
    {
        var partner = await Load().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (partner is null)
            return null;

        var categoryNames = await GetCategoryNamesAsync(partner.CategoryMap.Select(r => r.CategoryId).Distinct().ToList(), ct);
        var productIds = partner.ProductMap.Select(r => r.ProductId).Distinct().ToList();
        var products = await (
            from p in db.DdProducts
            where productIds.Contains(p.ProductId)
            join pd in db.DdProductDescriptions.Where(d => d.LanguageId == _languageId) on p.ProductId equals pd.ProductId into names
            from pd in names.DefaultIfEmpty()
            select new { p.ProductId, Name = pd == null ? "" : pd.Name, p.Status }
        ).ToDictionaryAsync(p => p.ProductId, ct);

        return new PartnerDto(partner.Id, partner.Code, partner.Name, partner.IsActive, partner.FeedUrl, partner.FeedEnabled,
            partner.VendorName, partner.Notes, partner.DefaultQuantity, partner.DateModified,
            partner.CategoryMap.OrderBy(r => r.Id)
                .Select(r => new PartnerCategoryMapDto(r.Id, r.CategoryId, categoryNames.GetValueOrDefault(r.CategoryId, ""), r.SizeName,
                    r.ExternalId, r.ExternalName, r.PercentSupply, r.PercentSale))
                .ToList(),
            partner.ProductMap.OrderBy(r => r.Id)
                .Select(r => new PartnerProductMapDto(r.Id, r.ProductId,
                    products.TryGetValue(r.ProductId, out var p) ? p.Name : "", p?.Status ?? false,
                    r.SizeName, r.ExternalId, r.ExternalName, r.PriceSupply, r.PriceSale))
                .ToList());
    }

    public Task<Partner?> GetAsync(int id, CancellationToken ct) => Load().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Partner?> GetByCodeAsync(string code, CancellationToken ct) =>
        Load().AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, ct);

    public Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct) =>
        db.Partners.AnyAsync(p => p.Code == code && (exceptId == null || p.Id != exceptId), ct);

    public void Add(Partner partner) => db.Partners.Add(partner);

    public void Remove(Partner partner) => db.Partners.Remove(partner);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<ShopCategoryDto>> ListCategoriesAsync(CancellationToken ct)
    {
        var categories = await db.DdCategoryDescriptions.AsNoTracking()
            .Where(c => c.LanguageId == _languageId)
            .OrderBy(c => c.Name)
            .Select(c => new { c.CategoryId, c.Name })
            .ToListAsync(ct);

        var sizes = await (
            from ptc in db.DdProductToCategories
            join size in SizeValues() on ptc.ProductId equals size.ProductId
            select new { ptc.CategoryId, size.Name }
        ).Distinct().ToListAsync(ct);
        var sizesByCategory = sizes.ToLookup(s => s.CategoryId, s => s.Name);

        return categories
            .Select(c => new ShopCategoryDto(c.CategoryId, c.Name, sizesByCategory[c.CategoryId].Order(StringComparer.Ordinal).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<PartnerProductPickDto>> SearchProductsAsync(string term, int limit, CancellationToken ct)
    {
        var id = int.TryParse(term, out var parsed) ? parsed : 0;
        var products = await (
            from p in db.DdProducts
            join pd in db.DdProductDescriptions on p.ProductId equals pd.ProductId
            where pd.LanguageId == _languageId && (pd.Name.Contains(term) || p.ProductId == id)
            orderby pd.Name
            select new { p.ProductId, pd.Name, p.Status, p.Price }
        ).Take(limit).ToListAsync(ct);

        var ids = products.Select(p => p.ProductId).ToList();
        var sizes = (await SizeValues().Where(s => ids.Contains(s.ProductId)).ToListAsync(ct)).ToLookup(s => s.ProductId);

        return products.Select(p => new PartnerProductPickDto(p.ProductId, p.Name, p.Status,
                sizes[p.ProductId].Any()
                    ? sizes[p.ProductId].DistinctBy(s => s.Name).OrderBy(s => s.Name, StringComparer.Ordinal).Select(s => new ProductSizePriceDto(s.Name, s.Price)).ToList()
                    : [new ProductSizePriceDto("", p.Price)]))
            .ToList();
    }

    public async Task<IReadOnlyList<FeedProduct>> GetFeedProductsAsync(
        IReadOnlyCollection<int> categoryIds, IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        // Проєкція, а не весь рядок dd_product: так не читаються «нульові» дати OpenCart.
        var products = await (
            from p in db.DdProducts
            join pd in db.DdProductDescriptions.Where(d => d.LanguageId == _languageId) on p.ProductId equals pd.ProductId into names
            from pd in names.DefaultIfEmpty()
            where p.Status && (productIds.Contains(p.ProductId)
                || db.DdProductToCategories.Any(c => c.ProductId == p.ProductId && categoryIds.Contains(c.CategoryId)))
            select new
            {
                p.ProductId, p.Model, p.Sku, p.Price, p.Image,
                Name = pd == null ? "" : pd.Name,
                Description = pd == null ? "" : pd.Description,
            }
        ).AsNoTracking().ToListAsync(ct);
        if (products.Count == 0)
            return [];

        var ids = products.Select(p => p.ProductId).ToList();
        var categories = (await db.DdProductToCategories.AsNoTracking().Where(c => ids.Contains(c.ProductId)).ToListAsync(ct))
            .ToLookup(c => c.ProductId, c => c.CategoryId);
        var images = (await db.DdProductImages.AsNoTracking().Where(i => ids.Contains(i.ProductId))
                .OrderBy(i => i.SortOrder).ThenBy(i => i.ProductImageId).ToListAsync(ct))
            .ToLookup(i => i.ProductId, i => i.Image ?? "");
        var optionValues = (await (
                from v in db.DdProductOptionValues
                where ids.Contains(v.ProductId)
                join d in db.DdOptionValueDescriptions.Where(d => d.LanguageId == _languageId) on v.OptionValueId equals d.OptionValueId into descriptions
                from d in descriptions.DefaultIfEmpty()
                orderby v.OptionValueId, v.ProductOptionValueId
                select new { v.ProductId, v.OptionValueId, Name = d == null ? "" : d.Name, v.Price }
            ).ToListAsync(ct))
            .ToLookup(v => v.ProductId, v => new FeedProductOption(v.OptionValueId, v.Name, v.Price));

        return products.Select(p => new FeedProduct(
                p.ProductId, p.Model, p.Sku, p.Price, p.Name, p.Description,
                categories[p.ProductId].Order().ToList(),
                (string.IsNullOrEmpty(p.Image) ? [] : new[] { p.Image }).Concat(images[p.ProductId]).ToList(),
                optionValues[p.ProductId].ToList()))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetCategoryNamesAsync(IReadOnlyCollection<int> categoryIds, CancellationToken ct) =>
        await db.DdCategoryDescriptions.AsNoTracking()
            .Where(c => c.LanguageId == _languageId && categoryIds.Contains(c.CategoryId))
            .ToDictionaryAsync(c => c.CategoryId, c => c.Name, ct);

    private IQueryable<Partner> Load() => db.Partners.Include(p => p.CategoryMap).Include(p => p.ProductMap);

    /// <summary>Розміри товарів: значення опцій, назва яких містить «Розмір», з ціною розміру.</summary>
    private IQueryable<SizeValue> SizeValues() =>
        from v in db.DdProductOptionValues
        join od in db.DdOptionDescriptions on v.OptionId equals od.OptionId
        join vd in db.DdOptionValueDescriptions on v.OptionValueId equals vd.OptionValueId
        where od.LanguageId == _languageId && vd.LanguageId == _languageId && od.Name.Contains(SizeOptionName)
        select new SizeValue { ProductId = v.ProductId, Name = vd.Name, Price = v.Price };

    // Клас з init-властивостями, а не позиційний record: EF перекладає в SQL подальші join і Distinct лише над такою проєкцією.
    private sealed class SizeValue
    {
        public int ProductId { get; init; }

        public string Name { get; init; } = "";

        public decimal Price { get; init; }
    }
}
