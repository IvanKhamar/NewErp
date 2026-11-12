using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Domain.Catalog;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Catalog;

/// <summary>
/// Форма товару над таблицями OpenCart, як їх писали addProduct / editProduct старої адмінки (Python first, пункти 43–45, 76).
/// MyISAM не має транзакцій: спершу захищений UPDATE dd_product (це перевірка версії), потім кожна пов'язана таблиця
/// замінюється. Збій посередині лишає товар зі старими зв'язками, і повторне збереження це виправляє.
/// </summary>
public sealed class ProductEditStore(ErpDbContext db, IOptions<ErpOptions> options) : IProductEditStore
{
    /// <summary>Опція, значення якої — розміри (так її знаходять і фіди партнерів).</summary>
    public const string SizeOptionName = "Розмір";

    /// <summary>Група фільтрів «За темою», де живе фільтр NEW нового принта (константа старої адмінки).</summary>
    public const int ThemeFilterGroupId = 7;

    private const int StoreId = 0;

    // «Без межі» для дат акції: MySQL у strict-режимі не приймає '0000-00-00'.
    private static readonly DateOnly NoStart = new(1000, 1, 1);
    private static readonly DateOnly NoEnd = new(9999, 12, 31);

    private readonly int _languageId = options.Value.LanguageId;

    public async Task<ProductEditDto?> GetAsync(int productId, CancellationToken ct)
    {
        var p = await db.DdProducts.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId, ct);
        if (p is null)
            return null;
        var d = await db.Database.SqlQuery<DescriptionRow>($"""
            SELECT name AS Name, description AS Description, tag AS Tag, meta_title AS MetaTitle,
                   meta_description AS MetaDescription, meta_keyword AS MetaKeyword, table_size AS TableSize, variant AS Variant,
                   notice AS Notice, model_info AS ModelInfo, characteristics AS Characteristics
            FROM dd_product_description WHERE product_id = {productId} AND language_id = {_languageId}
            """).FirstOrDefaultAsync(ct) ?? new DescriptionRow();
        var color = await db.ProductColors.AsNoTracking()
            .Where(c => c.ProductId == productId && c.LanguageId == _languageId).Select(c => c.Name).FirstOrDefaultAsync(ct);
        // Нульова дата й межі «без межі» у формі — порожні.
        var specials = await db.Database.SqlQuery<SpecialRow>($"""
            SELECT customer_group_id AS CustomerGroupId, priority AS Priority, price AS Price, percent AS Percent,
                   CASE WHEN date_start < '1000-01-02' THEN NULL ELSE date_start END AS DateStart,
                   CASE WHEN date_end < '1000-01-02' OR date_end = '9999-12-31' THEN NULL ELSE date_end END AS DateEnd
            FROM dd_product_special WHERE product_id = {productId} ORDER BY priority, product_special_id
            """).ToListAsync(ct);
        var (sizes, productOptions) = await SizesAndOptionsAsync(productId, ct);
        var images = await db.DdProductImages.AsNoTracking().Where(i => i.ProductId == productId).OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Image ?? "", i.SortOrder)).ToListAsync(ct);
        var query = ProductQuery(productId);
        var keyword = await db.SeoUrls.AsNoTracking()
            .Where(s => s.Query == query && s.StoreId == StoreId && s.LanguageId == _languageId).Select(s => s.Keyword).FirstOrDefaultAsync(ct);

        return new ProductEditDto
        {
            Id = p.ProductId, DateModified = p.DateModified, Model = p.Model, Sku = p.Sku, Price = p.Price, Minimum = p.Minimum,
            Subtract = p.Subtract ?? true, StockStatusId = p.StockStatusId, ManufacturerId = p.ManufacturerId, Weight = p.Weight,
            Length = p.Length, Width = p.Width, Height = p.Height, SortOrder = p.SortOrder, Enabled = p.Status, Image = p.Image ?? "",
            Name = d.Name ?? "", Description = d.Description ?? "", Tag = d.Tag ?? "", MetaTitle = d.MetaTitle ?? "",
            MetaDescription = d.MetaDescription ?? "", MetaKeyword = d.MetaKeyword ?? "", TableSize = d.TableSize ?? "",
            Variant = d.Variant ?? "", Notice = d.Notice ?? "", ModelInfo = d.ModelInfo ?? "", Characteristics = d.Characteristics ?? "",
            Color = color ?? "", SeoKeyword = keyword ?? "",
            CategoryIds = await db.DdProductToCategories.Where(x => x.ProductId == productId).Select(x => x.CategoryId).ToListAsync(ct),
            LabelIds = await db.DdProductToLabels.Where(x => x.ProductId == productId).Select(x => x.LabelId).ToListAsync(ct),
            FilterIds = await db.DdProductFilters.Where(x => x.ProductId == productId).Select(x => x.FilterId).ToListAsync(ct),
            RelatedIds = await db.ProductRelateds.Where(x => x.ProductId == productId).Select(x => x.RelatedId).ToListAsync(ct),
            RelatedColorIds = await db.ProductRelatedColors.Where(x => x.ProductId == productId).Select(x => x.RelatedId).ToListAsync(ct),
            RelatedVariantIds = await db.ProductRelatedVariants.Where(x => x.ProductId == productId).Select(x => x.RelatedId).ToListAsync(ct),
            Images = images,
            Specials = [.. specials.Select(s => new ProductSpecialDto(s.CustomerGroupId, s.Priority, s.Price, s.Percent, s.DateStart, s.DateEnd))],
            Sizes = sizes,
            Options = productOptions,
            Attributes = await db.ProductAttributes.Where(a => a.ProductId == productId && a.LanguageId == _languageId)
                .Select(a => new ProductAttributeDto(a.AttributeId, a.Text)).ToListAsync(ct),
            DownloadIds = await db.ProductToDownloads.Where(x => x.ProductId == productId).Select(x => x.DownloadId).ToListAsync(ct),
        };
    }

    public async Task<ProductFormLookups> LookupsAsync(CancellationToken ct)
    {
        var categories = await (
            from c in db.DdCategories
            join d in db.DdCategoryDescriptions on new { c.CategoryId, LanguageId = _languageId } equals new { d.CategoryId, d.LanguageId }
            where c.Status
            select new LookupItem(c.CategoryId, d.Name)).ToListAsync(ct);
        var ukrainian = StringComparer.Create(CultureInfo.GetCultureInfo("uk-UA"), ignoreCase: false);
        var labels = await db.DdProductLabels.OrderBy(l => l.SortOrder).Select(l => new LookupItem(l.LabelId, l.Name)).ToListAsync(ct);
        var filters = await (
            from f in db.DdFilters
            join d in db.DdFilterDescriptions on new { f.FilterId, LanguageId = _languageId } equals new { d.FilterId, d.LanguageId }
            orderby f.FilterGroupId, f.SortOrder
            select new { f.FilterId, d.Name, f.FilterGroupId }).ToListAsync(ct);
        var sizes = await (
            from v in db.DdOptionValueDescriptions
            join o in db.DdOptionDescriptions on v.OptionId equals o.OptionId
            where o.Name.Contains(SizeOptionName) && o.LanguageId == _languageId && v.LanguageId == _languageId
            orderby v.OptionValueId
            select new LookupItem(v.OptionValueId, v.Name)).ToListAsync(ct);
        var attributes = await (
            from a in db.CatalogAttributes
            join an in db.CatalogAttributeDescriptions.Where(x => x.LanguageId == _languageId) on a.AttributeId equals an.AttributeId into ans
            from an in ans.DefaultIfEmpty()
            join gn in db.CatalogAttributeGroupDescriptions.Where(x => x.LanguageId == _languageId) on a.AttributeGroupId equals gn.AttributeGroupId into gns
            from gn in gns.DefaultIfEmpty()
            orderby gn.Name, a.SortOrder
            select new { a.AttributeId, Name = an == null ? "" : an.Name, Group = gn == null ? null : gn.Name }).ToListAsync(ct);
        var downloads = await db.DownloadDescriptions.Where(d => d.LanguageId == _languageId).OrderBy(d => d.Name)
            .Select(d => new LookupItem(d.DownloadId, d.Name)).ToListAsync(ct);
        return new ProductFormLookups(
            [.. categories.OrderBy(c => c.Name, ukrainian)],
            labels,
            [.. filters.Select(f => new LookupItem(f.FilterId, $"{f.FilterGroupId}: {f.Name}"))],
            sizes,
            await OptionLookupsAsync(ct),
            [.. attributes.Select(a => new LookupItem(a.AttributeId, a.Group is null ? a.Name : $"{a.Group}: {a.Name}"))],
            downloads);
    }

    public async Task<IReadOnlySet<int>> ExistingProductsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct) =>
        productIds.Count == 0
            ? new HashSet<int>()
            : (await db.DdProducts.Where(p => productIds.Contains(p.ProductId)).Select(p => p.ProductId).ToListAsync(ct)).ToHashSet();

    public Task<string?> SeoKeywordOwnerAsync(string keyword, CancellationToken ct) =>
        db.SeoUrls.Where(s => s.Keyword == keyword && s.StoreId == StoreId).Select(s => (string?)s.Query).FirstOrDefaultAsync(ct);

    public async Task<int> CreateAsync(ProductForm form, DateTime now, CancellationToken ct)
    {
        var product = new DdProduct
        {
            Model = form.Model, Quantity = 0, DateAvailable = now.Date, DateAdded = now, DateModified = now, Shipping = true,
            Upc = "", Ean = "", Jan = "", Isbn = "", Mpn = "", Location = "",
        };
        Fill(product, form);
        db.DdProducts.Add(product);
        await db.SaveChangesAsync(ct);
        db.DdProductToStores.Add(new DdProductToStore { ProductId = product.ProductId, StoreId = StoreId });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        await WriteLinksAsync(product.ProductId, form, [], ct);
        return product.ProductId;
    }

    public async Task SaveAsync(int productId, ProductForm form, DateTime expected, DateTime now, CancellationToken ct)
    {
        var rows = await db.DdProducts
            .Where(p => p.ProductId == productId && p.DateModified == expected)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Model, form.Model)
                .SetProperty(p => p.Sku, form.Sku)
                .SetProperty(p => p.Price, form.Price)
                .SetProperty(p => p.Minimum, form.Minimum)
                .SetProperty(p => p.Subtract, form.Subtract)
                .SetProperty(p => p.StockStatusId, form.StockStatusId)
                .SetProperty(p => p.ManufacturerId, form.ManufacturerId)
                .SetProperty(p => p.Weight, form.Weight)
                .SetProperty(p => p.Length, form.Length)
                .SetProperty(p => p.Width, form.Width)
                .SetProperty(p => p.Height, form.Height)
                .SetProperty(p => p.SortOrder, form.SortOrder)
                .SetProperty(p => p.Status, form.Enabled)
                .SetProperty(p => p.Image, form.Image.Length > 0 ? form.Image : null)
                .SetProperty(p => p.DateModified, now), ct);
        if (rows == 0)
            throw new ConflictException(
                $"Товар №{productId} змінили в OpenCart або в іншому вікні після того, як ви його відкрили. Оновіть сторінку й повторіть зміни.");
        var before = await db.DdProductOptionValues.Where(v => v.ProductId == productId).Select(v => v.OptionValueId).ToListAsync(ct);
        await WriteLinksAsync(productId, form, [.. before], ct);
    }

    public async Task<RemoveNewResult> RemoveNewAsync(string sku, CancellationToken ct)
    {
        var productIds = await db.DdProducts.Where(p => p.Sku == sku).Select(p => p.ProductId).ToListAsync(ct);
        if (productIds.Count == 0)
            return new RemoveNewResult([], false, false);
        var labelId = await db.DdProductLabels.Where(l => l.Name == CatalogText.NewLabel).Select(l => (int?)l.LabelId).FirstOrDefaultAsync(ct);
        var filterId = await (
            from f in db.DdFilters
            join d in db.DdFilterDescriptions on f.FilterId equals d.FilterId
            where f.FilterGroupId == ThemeFilterGroupId && d.Name == CatalogText.NewLabel && d.LanguageId == _languageId
            select (int?)f.FilterId).FirstOrDefaultAsync(ct);
        if (labelId is { } label)
            await db.DdProductToLabels.Where(l => productIds.Contains(l.ProductId) && l.LabelId == label).ExecuteDeleteAsync(ct);
        if (filterId is { } filter)
            await db.DdProductFilters.Where(f => productIds.Contains(f.ProductId) && f.FilterId == filter).ExecuteDeleteAsync(ct);
        var tagged = await db.DdProductDescriptions.Where(d => productIds.Contains(d.ProductId) && d.Tag != "")
            .Select(d => new { d.ProductId, d.LanguageId, d.Tag }).ToListAsync(ct);
        foreach (var row in tagged)
        {
            var kept = CatalogText.WithoutNewTag(row.Tag);
            if (kept != row.Tag)
                await db.DdProductDescriptions.Where(d => d.ProductId == row.ProductId && d.LanguageId == row.LanguageId)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.Tag, kept), ct);
        }
        return new RemoveNewResult(productIds, labelId is not null, filterId is not null);
    }

    private static void Fill(DdProduct p, ProductForm form)
    {
        p.Sku = form.Sku;
        p.Price = form.Price;
        p.Minimum = form.Minimum;
        p.Subtract = form.Subtract;
        p.StockStatusId = form.StockStatusId;
        p.ManufacturerId = form.ManufacturerId;
        p.Weight = form.Weight;
        p.Length = form.Length;
        p.Width = form.Width;
        p.Height = form.Height;
        p.SortOrder = form.SortOrder;
        p.Status = form.Enabled;
        p.Image = form.Image.Length > 0 ? form.Image : null;
    }

    private async Task WriteLinksAsync(int productId, ProductForm form, HashSet<int> sizesBefore, CancellationToken ct)
    {
        // Опис разом із власними колонками старого сайту, яких немає в згенерованому класі.
        await db.DdProductDescriptions.Where(d => d.ProductId == productId && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dd_product_description (product_id, language_id, name, description, tag, meta_title, meta_description,
                meta_keyword, table_size, variant, notice, model_info, characteristics)
            VALUES ({productId}, {_languageId}, {form.Name}, {form.Description}, {form.Tag}, {form.MetaTitle}, {form.MetaDescription},
                {form.MetaKeyword}, {form.TableSize}, {form.Variant}, {form.Notice}, {form.ModelInfo}, {form.Characteristics})
            """, ct);
        await db.ProductColors.Where(c => c.ProductId == productId && c.LanguageId == _languageId).ExecuteDeleteAsync(ct);
        if (form.Color.Trim().Length > 0)
            db.ProductColors.Add(new ProductColor { ProductId = productId, LanguageId = _languageId, Name = form.Color });

        await db.DdProductToCategories.Where(x => x.ProductId == productId).ExecuteDeleteAsync(ct);
        db.DdProductToCategories.AddRange(form.CategoryIds.Distinct().Select(id => new DdProductToCategory { ProductId = productId, CategoryId = id }));
        await db.DdProductToLabels.Where(x => x.ProductId == productId).ExecuteDeleteAsync(ct);
        db.DdProductToLabels.AddRange(form.LabelIds.Distinct().Select(id => new DdProductToLabel { ProductId = productId, LabelId = id }));
        await db.DdProductFilters.Where(x => x.ProductId == productId).ExecuteDeleteAsync(ct);
        db.DdProductFilters.AddRange(form.FilterIds.Distinct().Select(id => new DdProductFilter { ProductId = productId, FilterId = id }));

        await db.ProductRelateds.Where(x => x.ProductId == productId).ExecuteDeleteAsync(ct);
        db.ProductRelateds.AddRange(form.RelatedIds.Select(id => new ProductRelated { ProductId = productId, RelatedId = id }));
        // Кольори й варіанти старого сайту йдуть в обидва боки: товар пов'язаний зі своїми кольорами, а вони — з ним.
        await db.ProductRelatedColors.Where(x => x.ProductId == productId || x.RelatedId == productId).ExecuteDeleteAsync(ct);
        db.ProductRelatedColors.AddRange(form.RelatedColorIds.SelectMany(id => new[]
        {
            new ProductRelatedColor { ProductId = productId, RelatedId = id }, new ProductRelatedColor { ProductId = id, RelatedId = productId },
        }));
        await db.ProductRelatedVariants.Where(x => x.ProductId == productId || x.RelatedId == productId).ExecuteDeleteAsync(ct);
        db.ProductRelatedVariants.AddRange(form.RelatedVariantIds.SelectMany(id => new[]
        {
            new ProductRelatedVariant { ProductId = productId, RelatedId = id }, new ProductRelatedVariant { ProductId = id, RelatedId = productId },
        }));

        await db.DdProductImages.Where(x => x.ProductId == productId).ExecuteDeleteAsync(ct);
        db.DdProductImages.AddRange(form.Images.Select(i => new DdProductImage { ProductId = productId, Image = i.Image, SortOrder = i.SortOrder }));
        await db.ProductSpecials.Where(x => x.ProductId == productId).ExecuteDeleteAsync(ct);
        db.ProductSpecials.AddRange(form.Specials.Select(s => new ProductSpecial
        {
            ProductId = productId, CustomerGroupId = s.CustomerGroupId, Priority = s.Priority, Price = s.Price, Percent = s.Percent,
            DateStart = s.DateStart ?? NoStart, DateEnd = s.DateEnd ?? NoEnd,
        }));

        var query = ProductQuery(productId);
        await db.SeoUrls.Where(s => s.Query == query && s.StoreId == StoreId).ExecuteDeleteAsync(ct);
        if (form.SeoKeyword.Length > 0)
            db.SeoUrls.Add(new SeoUrl { StoreId = StoreId, LanguageId = _languageId, Query = query, Keyword = form.SeoKeyword });
        await db.ProductAttributes.Where(a => a.ProductId == productId && a.LanguageId == _languageId).ExecuteDeleteAsync(ct);
        db.ProductAttributes.AddRange(form.Attributes.Select(a => new ProductAttribute
        {
            ProductId = productId, AttributeId = a.AttributeId, LanguageId = _languageId, Text = a.Text,
        }));
        await db.ProductToDownloads.Where(x => x.ProductId == productId).ExecuteDeleteAsync(ct);
        db.ProductToDownloads.AddRange(form.DownloadIds.Select(id => new ProductToDownload { ProductId = productId, DownloadId = id }));
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        await WriteOptionsAsync(productId, form, sizesBefore, ct);
    }

    /// <summary>
    /// Змінює ціни, додає нові значення без залишку й прибирає пропущені; залишок лишається, як його поставило виробництво.
    /// Рядки змінюються на місці й ніколи не переписуються: на них посилаються рядки старих замовлень.
    /// </summary>
    private async Task WriteOptionsAsync(int productId, ProductForm form, HashSet<int> before, CancellationToken ct)
    {
        var wanted = form.Sizes.Select(s => s.OptionValueId).Concat(form.Options.SelectMany(o => o.Values).Select(v => v.OptionValueId)).ToHashSet();
        var gone = before.Where(id => !wanted.Contains(id)).ToList();
        if (gone.Count > 0)
            await db.DdProductOptionValues.Where(v => v.ProductId == productId && gone.Contains(v.OptionValueId)).ExecuteDeleteAsync(ct);

        foreach (var size in form.Sizes)
        {
            if (before.Contains(size.OptionValueId))
            {
                await db.DdProductOptionValues.Where(v => v.ProductId == productId && v.OptionValueId == size.OptionValueId)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.Price, size.Price), ct);
                continue;
            }
            var optionId = await db.DdOptionValueDescriptions.Where(v => v.OptionValueId == size.OptionValueId)
                .Select(v => v.OptionId).FirstAsync(ct);
            var parent = await WriteParentAsync(productId, optionId, required: true, "", ct);
            // subtract: вітрина зменшує залишок і ховає розмір на нулі, доки виробництво не додасть.
            await InsertValueAsync(productId, parent, optionId, size.OptionValueId, size.Price, "+", subtract: true, ct);
        }
        foreach (var chosen in form.Options)
        {
            var parent = await WriteParentAsync(productId, chosen.OptionId, chosen.Required, chosen.Value, ct);
            foreach (var value in chosen.Values)
            {
                if (before.Contains(value.OptionValueId))
                    await db.DdProductOptionValues.Where(v => v.ProductId == productId && v.OptionValueId == value.OptionValueId)
                        .ExecuteUpdateAsync(s => s.SetProperty(v => v.Price, value.Price).SetProperty(v => v.PricePrefix, value.PricePrefix), ct);
                else
                    // Виробництво веде залишок лише за розміром, тож колір не лічимо: інакше він ховався б на нулі.
                    await InsertValueAsync(productId, parent, chosen.OptionId, value.OptionValueId, value.Price, value.PricePrefix, subtract: false, ct);
            }
        }
        // Опція, в якої нічого не лишилось, теж іде: вітрина питала б про вибір, якого немає.
        var kept = (await db.DdProductOptionValues.Where(v => v.ProductId == productId).Select(v => v.OptionId).ToListAsync(ct))
            .Concat(form.Options.Select(o => o.OptionId)).Distinct().ToList();
        await db.DdProductOptions.Where(o => o.ProductId == productId && !kept.Contains(o.OptionId)).ExecuteDeleteAsync(ct);
        // Кількість товару — сума його рядків опцій, як її веде виробництво.
        if (wanted.Count > 0 || before.Count > 0)
        {
            var total = await db.DdProductOptionValues.Where(v => v.ProductId == productId).SumAsync(v => (int?)v.Quantity, ct) ?? 0;
            await db.DdProducts.Where(p => p.ProductId == productId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Quantity, total), ct);
        }
    }

    private async Task<int> WriteParentAsync(int productId, int optionId, bool required, string value, CancellationToken ct)
    {
        var parent = await db.DdProductOptions.Where(o => o.ProductId == productId && o.OptionId == optionId)
            .Select(o => (int?)o.ProductOptionId).FirstOrDefaultAsync(ct);
        if (parent is { } id)
        {
            await db.DdProductOptions.Where(o => o.ProductOptionId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Value, value).SetProperty(o => o.Required, required), ct);
            return id;
        }
        var row = new DdProductOption { ProductId = productId, OptionId = optionId, Value = value, Required = required };
        db.DdProductOptions.Add(row);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return row.ProductOptionId;
    }

    private async Task InsertValueAsync(
        int productId, int parent, int optionId, int optionValueId, decimal price, string prefix, bool subtract, CancellationToken ct)
    {
        db.DdProductOptionValues.Add(new DdProductOptionValue
        {
            ProductOptionId = parent, ProductId = productId, OptionId = optionId, OptionValueId = optionValueId, Quantity = 0,
            Subtract = subtract, Price = price, PricePrefix = prefix, Points = 0, PointsPrefix = "+", Weight = 0, WeightPrefix = "+",
        });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private async Task<HashSet<int>> SizeOptionIdsAsync(CancellationToken ct) =>
        (await db.DdOptionDescriptions.Where(d => d.Name.Contains(SizeOptionName) && d.LanguageId == _languageId)
            .Select(d => d.OptionId).ToListAsync(ct)).ToHashSet();

    /// <summary>Рядки опцій товару: опції «Розмір» — його розміри, решта — інші опції.</summary>
    private async Task<(List<ProductSizeEditDto> Sizes, List<ProductOptionDto> Options)> SizesAndOptionsAsync(int productId, CancellationToken ct)
    {
        var sizeOptions = await SizeOptionIdsAsync(ct);
        var parents = await (
            from o in db.DdProductOptions
            join d in db.DdOptionDescriptions.Where(x => x.LanguageId == _languageId) on o.OptionId equals d.OptionId into ds
            from d in ds.DefaultIfEmpty()
            where o.ProductId == productId
            orderby o.ProductOptionId
            select new { o.OptionId, Name = d == null ? null : d.Name, o.Value, o.Required }).ToListAsync(ct);
        var options = new Dictionary<int, (ProductOptionDto Option, List<ProductOptionValueEditDto> Values)>();
        foreach (var o in parents.Where(o => !sizeOptions.Contains(o.OptionId)))
            options.TryAdd(o.OptionId, (new ProductOptionDto { OptionId = o.OptionId, Name = o.Name ?? $"#{o.OptionId}", Value = o.Value ?? "", Required = o.Required }, []));
        var rows = await (
            from v in db.DdProductOptionValues
            join n in db.DdOptionValueDescriptions.Where(x => x.LanguageId == _languageId) on v.OptionValueId equals n.OptionValueId into ns
            from n in ns.DefaultIfEmpty()
            where v.ProductId == productId
            orderby v.ProductOptionValueId
            select new { v.OptionId, v.OptionValueId, Name = n == null ? null : n.Name, v.Price, v.PricePrefix, v.Quantity }).ToListAsync(ct);
        var sizes = new List<ProductSizeEditDto>();
        foreach (var r in rows)
        {
            if (sizeOptions.Contains(r.OptionId))
            {
                sizes.Add(new ProductSizeEditDto(r.OptionValueId, r.Name ?? $"#{r.OptionValueId}", r.Price, r.Quantity));
                continue;
            }
            if (!options.TryGetValue(r.OptionId, out var entry))
                options[r.OptionId] = entry = (new ProductOptionDto { OptionId = r.OptionId, Name = $"#{r.OptionId}" }, []);
            entry.Values.Add(new ProductOptionValueEditDto(r.OptionValueId, r.Name ?? $"#{r.OptionValueId}", r.Price, r.PricePrefix == "-" ? "-" : "+", r.Quantity));
        }
        return (sizes, [.. options.Values.Select(o => o.Option with { Values = o.Values })]);
    }

    private async Task<IReadOnlyList<ProductOptionLookup>> OptionLookupsAsync(CancellationToken ct)
    {
        var sizeOptions = await SizeOptionIdsAsync(ct);
        var values = (await (
                from v in db.CatalogOptionValues
                join n in db.DdOptionValueDescriptions.Where(x => x.LanguageId == _languageId) on v.OptionValueId equals n.OptionValueId into ns
                from n in ns.DefaultIfEmpty()
                orderby v.SortOrder, v.OptionValueId
                select new { v.OptionId, v.OptionValueId, Name = n == null ? null : n.Name }).ToListAsync(ct))
            .ToLookup(v => v.OptionId, v => new LookupItem(v.OptionValueId, v.Name ?? $"#{v.OptionValueId}"));
        var options = await (
            from o in db.CatalogOptions
            join d in db.DdOptionDescriptions.Where(x => x.LanguageId == _languageId) on o.OptionId equals d.OptionId into ds
            from d in ds.DefaultIfEmpty()
            orderby o.SortOrder, o.OptionId
            select new { o.OptionId, Name = d == null ? null : d.Name, o.Type }).ToListAsync(ct);
        return [.. options.Where(o => !sizeOptions.Contains(o.OptionId)).Select(o => new ProductOptionLookup(
            o.OptionId, o.Name ?? $"#{o.OptionId}", o.Type, ProductEditService.OptionTypesWithValues.Contains(o.Type), [.. values[o.OptionId]]))];
    }

    private static string ProductQuery(int productId) => $"product_id={productId}";

    private sealed class DescriptionRow
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Tag { get; set; }

        public string? MetaTitle { get; set; }

        public string? MetaDescription { get; set; }

        public string? MetaKeyword { get; set; }

        public string? TableSize { get; set; }

        public string? Variant { get; set; }

        public string? Notice { get; set; }

        public string? ModelInfo { get; set; }

        public string? Characteristics { get; set; }
    }

    private sealed class SpecialRow
    {
        public int CustomerGroupId { get; set; }

        public int Priority { get; set; }

        public decimal Price { get; set; }

        public decimal Percent { get; set; }

        public DateOnly? DateStart { get; set; }

        public DateOnly? DateEnd { get; set; }
    }
}
