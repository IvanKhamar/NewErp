using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Catalog;

/// <summary>
/// Мітки, пресети пошуку й категорії над таблицями OpenCart, як їх писала стара адмінка (пункти 47–48).
/// Категорії тримають таблицю-замикання dd_category_path (кожна категорія з кожним предком і рівнем): вітрина бере з
/// неї хлібні крихти й товари підкатегорій, тож після кожного збереження її перебудовуємо з parent_id, як старий «repair».
/// </summary>
public sealed class CatalogDictionaryStore(ErpDbContext db, IOptions<ErpOptions> options) : ICatalogDictionaryStore
{
    private const int StoreId = 0;
    private static readonly StringComparer Names = StringComparer.Create(CultureInfo.GetCultureInfo("uk-UA"), ignoreCase: true);

    private readonly int _languageId = options.Value.LanguageId;

    private static string CategoryQuery(int categoryId) => $"category_id={categoryId}";

    // ═══ Мітки ═══

    public async Task<IReadOnlyList<LabelDto>> LabelsAsync(CancellationToken ct) =>
        await db.DdProductLabels.AsNoTracking()
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
            .Select(l => new LabelDto(l.LabelId, l.Name, l.BackgroundColor, l.TextColor, l.SortOrder, l.Status ?? true,
                db.DdProductToLabels.Count(p => p.LabelId == l.LabelId)))
            .ToListAsync(ct);

    public async Task<int> SaveLabelAsync(LabelDto label, CancellationToken ct)
    {
        var row = label.Id == 0 ? db.DdProductLabels.Add(new DdProductLabel()).Entity : await db.DdProductLabels.SingleAsync(l => l.LabelId == label.Id, ct);
        (row.Name, row.BackgroundColor, row.TextColor, row.SortOrder, row.Status) = (label.Name, label.BackgroundColor, label.TextColor, label.SortOrder, label.Enabled);
        await db.SaveChangesAsync(ct);
        return row.LabelId;
    }

    public async Task<bool> DeleteLabelAsync(int labelId, CancellationToken ct)
    {
        var deleted = await db.DdProductLabels.Where(l => l.LabelId == labelId).ExecuteDeleteAsync(ct);
        await db.DdProductToLabels.Where(p => p.LabelId == labelId).ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    // ═══ Пресети пошуку ═══

    public async Task<IReadOnlyList<SearchPresetDto>> PresetsAsync(CancellationToken ct) =>
        await db.SearchPresets.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new SearchPresetDto(p.Id, p.Name, p.Url, p.SortOrder, p.Status)).ToListAsync(ct);

    public async Task<int> SavePresetAsync(SearchPresetDto preset, CancellationToken ct)
    {
        var row = preset.Id == 0 ? db.SearchPresets.Add(new SearchPreset()).Entity : await db.SearchPresets.SingleAsync(p => p.Id == preset.Id, ct);
        (row.Name, row.Url, row.SortOrder, row.Status) = (preset.Name, preset.Url, preset.SortOrder, preset.Enabled);
        await db.SaveChangesAsync(ct);
        return row.Id;
    }

    public async Task<bool> DeletePresetAsync(int presetId, CancellationToken ct) =>
        await db.SearchPresets.Where(p => p.Id == presetId).ExecuteDeleteAsync(ct) > 0;

    // ═══ Категорії ═══

    public async Task<IReadOnlyList<CategoryListItem>> CategoriesAsync(CancellationToken ct)
    {
        var rows = await (
            from c in db.DdCategories.AsNoTracking()
            join d in db.DdCategoryDescriptions.Where(x => x.LanguageId == _languageId) on c.CategoryId equals d.CategoryId into ds
            from d in ds.DefaultIfEmpty()
            select new
            {
                c.CategoryId, Name = d == null ? "" : d.Name, c.ParentId, c.SortOrder, c.Status,
                Products = db.DdProductToCategories.Count(p => p.CategoryId == c.CategoryId),
            }).ToListAsync(ct);
        var names = rows.ToDictionary(r => r.CategoryId, r => r.Name.Length > 0 ? r.Name : $"#{r.CategoryId}");
        var parents = rows.ToDictionary(r => r.CategoryId, r => r.ParentId);

        string FullName(int categoryId)
        {
            var parts = new List<string>();
            var seen = new HashSet<int>();
            for (var current = categoryId; names.ContainsKey(current) && seen.Add(current); current = parents[current])
                parts.Add(names[current]);
            parts.Reverse();
            return string.Join(" > ", parts);
        }

        return [.. rows.Select(r => new CategoryListItem(r.CategoryId, FullName(r.CategoryId), r.ParentId, r.SortOrder, r.Status, r.Products))
            .OrderBy(c => c.Name, Names)];
    }

    public async Task<CategoryEditDto?> CategoryAsync(int categoryId, CancellationToken ct)
    {
        // Власні колонки старого сайту (банер, картинка таблиці розмірів, назви товару) — поза згенерованими класами.
        var c = await db.Database.SqlQuery<CategoryRow>($"""
            SELECT parent_id AS ParentId, image AS Image, image_table_size AS ImageTableSize, top AS Top, `column` AS `Column`,
                   sort_order AS SortOrder, status AS Status, production_period AS ProductionPeriod, banner_id AS BannerId
            FROM dd_category WHERE category_id = {categoryId}
            """).FirstOrDefaultAsync(ct);
        if (c is null)
            return null;
        var d = await db.Database.SqlQuery<DescriptionRow>($"""
            SELECT name AS Name, product_name AS ProductName, advertising_product_name AS AdvertisingProductName,
                   description AS Description, description_table_size AS DescriptionTableSize, meta_title AS MetaTitle,
                   meta_description AS MetaDescription, meta_keyword AS MetaKeyword
            FROM dd_category_description WHERE category_id = {categoryId} AND language_id = {_languageId}
            """).FirstOrDefaultAsync(ct) ?? new DescriptionRow();
        var query = CategoryQuery(categoryId);
        var keyword = await db.SeoUrls.AsNoTracking().Where(s => s.Query == query && s.StoreId == StoreId).Select(s => s.Keyword).FirstOrDefaultAsync(ct);
        return new CategoryEditDto
        {
            Id = categoryId, ParentId = c.ParentId, Image = c.Image ?? "", ImageTableSize = c.ImageTableSize ?? "", Top = c.Top,
            Column = c.Column, SortOrder = c.SortOrder, Enabled = c.Status, ProductionPeriod = c.ProductionPeriod, BannerId = c.BannerId ?? 0,
            Name = d.Name ?? "", ProductName = d.ProductName ?? "", AdvertisingProductName = d.AdvertisingProductName ?? "",
            Description = d.Description ?? "", DescriptionTableSize = d.DescriptionTableSize ?? "", MetaTitle = d.MetaTitle ?? "",
            MetaDescription = d.MetaDescription ?? "", MetaKeyword = d.MetaKeyword ?? "", SeoKeyword = keyword ?? "",
            FilterIds = await db.CategoryFilters.Where(f => f.CategoryId == categoryId).Select(f => f.FilterId).ToListAsync(ct),
        };
    }

    public async Task<IReadOnlyList<LookupItem>> FiltersAsync(CancellationToken ct) =>
        await (
            from f in db.DdFilters.AsNoTracking()
            join d in db.DdFilterDescriptions.Where(x => x.LanguageId == _languageId) on f.FilterId equals d.FilterId
            orderby f.FilterGroupId, f.SortOrder
            select new LookupItem(f.FilterId, f.FilterGroupId + ": " + d.Name)).ToListAsync(ct);

    public Task<string?> SeoKeywordOwnerAsync(string keyword, CancellationToken ct) =>
        db.SeoUrls.Where(s => s.Keyword == keyword && s.StoreId == StoreId).Select(s => (string?)s.Query).FirstOrDefaultAsync(ct);

    public async Task<int> SaveCategoryAsync(int? categoryId, CategoryFormDto form, DateTime now, CancellationToken ct)
    {
        string? image = form.Image.Length > 0 ? form.Image : null, tableImage = form.ImageTableSize.Length > 0 ? form.ImageTableSize : null;
        int id;
        if (categoryId is { } existing)
        {
            id = existing;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dd_category SET parent_id = {form.ParentId}, image = {image}, image_table_size = {tableImage}, top = {form.Top},
                    `column` = {form.Column}, sort_order = {form.SortOrder}, status = {form.Enabled}, production_period = {form.ProductionPeriod},
                    banner_id = {form.BannerId}, date_modified = {now}
                WHERE category_id = {id}
                """, ct);
        }
        else
        {
            var category = new DdCategory
            {
                ParentId = form.ParentId, Image = image, Top = form.Top, Column = form.Column, SortOrder = form.SortOrder,
                Status = form.Enabled, ProductionPeriod = form.ProductionPeriod, DateAdded = now, DateModified = now,
            };
            db.DdCategories.Add(category);
            await db.SaveChangesAsync(ct);
            id = category.CategoryId;
            db.CategoryToStores.Add(new CategoryToStore { CategoryId = id, StoreId = StoreId });
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dd_category SET image_table_size = {tableImage}, banner_id = {form.BannerId} WHERE category_id = {id}", ct);
        }

        await db.DdCategoryDescriptions.Where(d => d.CategoryId == id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dd_category_description (category_id, language_id, name, product_name, advertising_product_name, description,
                description_table_size, meta_title, meta_description, meta_keyword)
            VALUES ({id}, {_languageId}, {form.Name}, {form.ProductName}, {form.AdvertisingProductName}, {form.Description},
                {form.DescriptionTableSize}, {form.MetaTitle}, {form.MetaDescription}, {form.MetaKeyword})
            """, ct);
        await db.CategoryFilters.Where(f => f.CategoryId == id).ExecuteDeleteAsync(ct);
        db.CategoryFilters.AddRange(form.FilterIds.Select(f => new CategoryFilter { CategoryId = id, FilterId = f }));
        var query = CategoryQuery(id);
        await db.SeoUrls.Where(s => s.Query == query && s.StoreId == StoreId).ExecuteDeleteAsync(ct);
        if (form.SeoKeyword.Length > 0)
            db.SeoUrls.Add(new SeoUrl { StoreId = StoreId, LanguageId = _languageId, Query = query, Keyword = form.SeoKeyword });
        await db.SaveChangesAsync(ct);
        await RepairPathsAsync(ct);
        return id;
    }

    public Task<bool> HasChildrenAsync(int categoryId, CancellationToken ct) => db.DdCategories.AnyAsync(c => c.ParentId == categoryId, ct);

    public async Task DeleteCategoryAsync(int categoryId, CancellationToken ct)
    {
        await db.CategoryPaths.Where(x => x.CategoryId == categoryId).ExecuteDeleteAsync(ct);
        await db.CategoryFilters.Where(x => x.CategoryId == categoryId).ExecuteDeleteAsync(ct);
        await db.CategoryToStores.Where(x => x.CategoryId == categoryId).ExecuteDeleteAsync(ct);
        await db.DdProductToCategories.Where(x => x.CategoryId == categoryId).ExecuteDeleteAsync(ct);
        await db.CouponCategories.Where(x => x.CategoryId == categoryId).ExecuteDeleteAsync(ct);
        await db.CouponCategoryExcludes.Where(x => x.CategoryId == categoryId).ExecuteDeleteAsync(ct);
        await db.DdCategoryDescriptions.Where(x => x.CategoryId == categoryId).ExecuteDeleteAsync(ct);
        await db.DdCategories.Where(x => x.CategoryId == categoryId).ExecuteDeleteAsync(ct);
        var query = CategoryQuery(categoryId);
        await db.SeoUrls.Where(s => s.Query == query).ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Кожна категорія отримує своїх предків від кореня (рівень 0) і себе останньою. Зламаний ланцюжок (відсутній предок чи
    /// петля) обривається там, де ламається, тож перебудова завжди закінчується.
    /// </summary>
    public async Task<int> RepairPathsAsync(CancellationToken ct)
    {
        var parents = await db.DdCategories.AsNoTracking().ToDictionaryAsync(c => c.CategoryId, c => c.ParentId, ct);
        var rows = new List<CategoryPath>();
        foreach (var categoryId in parents.Keys)
        {
            var chain = new List<int>();
            var seen = new HashSet<int>();
            for (var current = categoryId; current != 0 && parents.ContainsKey(current) && seen.Add(current); current = parents[current])
                chain.Add(current);
            chain.Reverse();
            rows.AddRange(chain.Select((pathId, level) => new CategoryPath { CategoryId = categoryId, PathId = pathId, Level = level }));
        }
        await db.CategoryPaths.ExecuteDeleteAsync(ct);
        db.CategoryPaths.AddRange(rows);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return parents.Count;
    }

    private sealed class CategoryRow
    {
        public int ParentId { get; set; }

        public string? Image { get; set; }

        public string? ImageTableSize { get; set; }

        public bool Top { get; set; }

        public int Column { get; set; }

        public int SortOrder { get; set; }

        public bool Status { get; set; }

        public int ProductionPeriod { get; set; }

        public int? BannerId { get; set; }
    }

    private sealed class DescriptionRow
    {
        public string? Name { get; set; }

        public string? ProductName { get; set; }

        public string? AdvertisingProductName { get; set; }

        public string? Description { get; set; }

        public string? DescriptionTableSize { get; set; }

        public string? MetaTitle { get; set; }

        public string? MetaDescription { get; set; }

        public string? MetaKeyword { get; set; }
    }
}
