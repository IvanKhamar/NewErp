using System.Text.RegularExpressions;

using NewErp.Application.Common;
using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

using static NewErp.Application.Catalog.RequestLimits;

namespace NewErp.Application.Catalog;

// Довідники каталогу старої адмінки (Python first): мітки товарів і пресети пошуку вітрини (пункт 47), категорії з
// їхнім деревом (пункт 48).

/// <summary>Мітка товару. Products — скільки товарів її мають (лише для читання).</summary>
public sealed record LabelDto(
    int Id = 0, string Name = "", string BackgroundColor = "", string TextColor = "", int SortOrder = 0, bool Enabled = true, int Products = 0);

/// <summary>Готовий пошук вітрини: назва й адреса, яку він відкриває.</summary>
public sealed record SearchPresetDto(int Id = 0, string Name = "", string Url = "", int SortOrder = 0, bool Enabled = true);

/// <summary>Name — з батьками: «Одяг &gt; Сукні».</summary>
public sealed record CategoryListItem(int Id, string Name, int ParentId, int SortOrder, bool Enabled, int Products);

/// <summary>Категорія OpenCart із власними полями старого сайту (строк виготовлення, банер, картинка таблиці розмірів).</summary>
public record CategoryFormDto
{
    public int ParentId { get; init; }
    public string Name { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string AdvertisingProductName { get; init; } = "";
    public string Description { get; init; } = "";
    public string DescriptionTableSize { get; init; } = "";
    public string MetaTitle { get; init; } = "";
    public string MetaDescription { get; init; } = "";
    public string MetaKeyword { get; init; } = "";
    public string SeoKeyword { get; init; } = "";
    public string Image { get; init; } = "";
    public string ImageTableSize { get; init; } = "";
    public bool Top { get; init; }
    public int Column { get; init; } = 1;
    public int SortOrder { get; init; }
    public bool Enabled { get; init; } = true;
    public int ProductionPeriod { get; init; }
    public int BannerId { get; init; }
    public IReadOnlyList<int> FilterIds { get; init; } = [];
}

public sealed record CategoryEditDto : CategoryFormDto
{
    public int Id { get; init; }
}

public sealed record CategoryRepairResult(int Categories);

public interface ICatalogDictionaryStore
{
    Task<IReadOnlyList<LabelDto>> LabelsAsync(CancellationToken ct);

    Task<int> SaveLabelAsync(LabelDto label, CancellationToken ct);

    /// <summary>Знімає мітку й з усіх товарів; false — такої мітки не було.</summary>
    Task<bool> DeleteLabelAsync(int labelId, CancellationToken ct);

    Task<IReadOnlyList<SearchPresetDto>> PresetsAsync(CancellationToken ct);

    Task<int> SavePresetAsync(SearchPresetDto preset, CancellationToken ct);

    Task<bool> DeletePresetAsync(int presetId, CancellationToken ct);

    Task<IReadOnlyList<CategoryListItem>> CategoriesAsync(CancellationToken ct);

    Task<CategoryEditDto?> CategoryAsync(int categoryId, CancellationToken ct);

    /// <summary>Значення фільтрів як «група: значення».</summary>
    Task<IReadOnlyList<LookupItem>> FiltersAsync(CancellationToken ct);

    Task<string?> SeoKeywordOwnerAsync(string keyword, CancellationToken ct);

    /// <summary>Створює (null) чи змінює категорію й перебудовує її шлях у дереві та шляхи під нею.</summary>
    Task<int> SaveCategoryAsync(int? categoryId, CategoryFormDto form, DateTime now, CancellationToken ct);

    Task<bool> HasChildrenAsync(int categoryId, CancellationToken ct);

    Task DeleteCategoryAsync(int categoryId, CancellationToken ct);

    /// <summary>Перебудовує dd_category_path з parent_id для кожної категорії (старий «repair»); повертає їх кількість.</summary>
    Task<int> RepairPathsAsync(CancellationToken ct);
}

public sealed partial class CatalogDictionaryService(ICatalogDictionaryStore store, IHtmlCleaner html, TimeProvider clock, IAuditLog audit)
{
    private const int MaxFilters = 200;

    // ═══ Мітки ═══

    public Task<IReadOnlyList<LabelDto>> LabelsAsync(CancellationToken ct) => store.LabelsAsync(ct);

    public async Task<LabelDto> SaveLabelAsync(int? labelId, LabelDto label, CancellationToken ct)
    {
        Length("Назва мітки", label.Name, 64);
        Length("Колір фону", label.BackgroundColor, 16);
        Length("Колір тексту", label.TextColor, 16);
        var name = label.Name.Trim();
        if (name.Length == 0)
            throw new DomainException("Вкажіть назву мітки.");
        // Колір іде в атрибут style на вітрині: нічого іншого туди не можна.
        foreach (var color in new[] { label.BackgroundColor, label.TextColor })
            if (color.Length > 0 && !Color().IsMatch(color))
                throw new DomainException($"Колір «{color}» має бути на кшталт #ff8800.");
        if (labelId is { } id && (await store.LabelsAsync(ct)).All(l => l.Id != id))
            throw new NotFoundException($"Мітки {id} немає.");
        var savedId = await store.SaveLabelAsync(label with { Id = labelId ?? 0, Name = name }, ct);
        return (await store.LabelsAsync(ct)).First(l => l.Id == savedId);
    }

    public async Task DeleteLabelAsync(int labelId, int userId, CancellationToken ct)
    {
        if (!await store.DeleteLabelAsync(labelId, ct))
            throw new NotFoundException($"Мітки {labelId} немає.");
        audit.Record($"label deleted: label {labelId}, by user {userId}");
    }

    // ═══ Пресети пошуку ═══

    public Task<IReadOnlyList<SearchPresetDto>> PresetsAsync(CancellationToken ct) => store.PresetsAsync(ct);

    public async Task<SearchPresetDto> SavePresetAsync(int? presetId, SearchPresetDto preset, CancellationToken ct)
    {
        Length("Назва пресета", preset.Name, 255);
        Length("Адреса пресета", preset.Url, 255);
        var name = preset.Name.Trim();
        if (name.Length == 0)
            throw new DomainException("Вкажіть назву пресета.");
        var url = PresetUrl(preset.Url);
        if (presetId is { } id && (await store.PresetsAsync(ct)).All(p => p.Id != id))
            throw new NotFoundException($"Пресета {id} немає.");
        var savedId = await store.SavePresetAsync(preset with { Id = presetId ?? 0, Name = name, Url = url }, ct);
        return (await store.PresetsAsync(ct)).First(p => p.Id == savedId);
    }

    public async Task DeletePresetAsync(int presetId, CancellationToken ct)
    {
        if (!await store.DeletePresetAsync(presetId, ct))
            throw new NotFoundException($"Пресета {presetId} немає.");
    }

    /// <summary>
    /// Лише адреса самого магазину («/sukni?filter=…») або https: вітрина ставить її в href, тож javascript: і data:
    /// не проходять ніколи.
    /// </summary>
    public static string PresetUrl(string? value)
    {
        var url = (value ?? "").Trim();
        var local = url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal);
        if ((local || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            && !url.Any(c => c is '"' or '\'' or '<' or '>' or ' ' or '\\' || c < ' '))
            return url;
        throw new DomainException("Адреса пресета: відносна (/catalog?...) або https://..., без пробілів і лапок.");
    }

    // ═══ Категорії ═══

    public Task<IReadOnlyList<CategoryListItem>> CategoriesAsync(CancellationToken ct) => store.CategoriesAsync(ct);

    public Task<IReadOnlyList<LookupItem>> FiltersAsync(CancellationToken ct) => store.FiltersAsync(ct);

    public async Task<CategoryEditDto> CategoryAsync(int categoryId, CancellationToken ct) =>
        await store.CategoryAsync(categoryId, ct) ?? throw new NotFoundException($"Категорії {categoryId} немає.");

    public async Task<CategoryEditDto> SaveCategoryAsync(int? categoryId, CategoryFormDto form, int userId, CancellationToken ct)
    {
        CheckLimits(form);
        var (image, tableImage, keyword) = (CatalogText.ImagePath(form.Image), CatalogText.ImagePath(form.ImageTableSize), CatalogText.SeoKeyword(form.SeoKeyword));
        if (form.Name.Trim().Length == 0)
            throw new DomainException("Вкажіть назву категорії.");
        if (categoryId is { } id)
            await CategoryAsync(id, ct);
        var known = (await store.CategoriesAsync(ct)).ToDictionary(c => c.Id);
        if (form.ParentId != 0)
        {
            if (!known.ContainsKey(form.ParentId))
                throw new DomainException($"Батьківської категорії {form.ParentId} немає.");
            // Категорію не можна вкласти в саму себе чи в її підкатегорію: дерево стало б петлею.
            var (parent, seen) = (form.ParentId, new HashSet<int>());
            while (parent != 0 && seen.Add(parent))
            {
                if (parent == categoryId)
                    throw new DomainException("Категорію не можна вкласти в саму себе чи в її підкатегорію.");
                parent = known.TryGetValue(parent, out var item) ? item.ParentId : 0;
            }
        }
        var filterIds = form.FilterIds.Distinct().ToList();
        var unknown = filterIds.Except((await store.FiltersAsync(ct)).Select(f => f.Id)).Order().ToList();
        if (unknown.Count > 0)
            throw new DomainException($"Невідомі фільтри: {string.Join(", ", unknown)}.");
        if (keyword.Length > 0 && await store.SeoKeywordOwnerAsync(keyword, ct) is { } owner && owner != $"category_id={categoryId}")
            throw new ConflictException($"SEO-адресу «{keyword}» вже має {owner}.");

        var clean = form with
        {
            Name = form.Name.Trim(), Image = image, ImageTableSize = tableImage, SeoKeyword = keyword,
            Description = html.Clean(form.Description), DescriptionTableSize = html.Clean(form.DescriptionTableSize), FilterIds = filterIds,
        };
        var now = clock.GetLocalNow().DateTime;
        var savedId = await store.SaveCategoryAsync(categoryId, clean, now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond)), ct);
        audit.Record($"category saved: category {savedId}, by user {userId}");
        return await CategoryAsync(savedId, ct);
    }

    /// <summary>На відміну від старої адмінки, підкатегорії разом не видаляються: їх треба спершу перенести чи видалити.</summary>
    public async Task DeleteCategoryAsync(int categoryId, int userId, CancellationToken ct)
    {
        await CategoryAsync(categoryId, ct);
        if (await store.HasChildrenAsync(categoryId, ct))
            throw new DomainException("У категорії є підкатегорії: спершу перенесіть або видаліть їх.");
        await store.DeleteCategoryAsync(categoryId, ct);
        audit.Record($"category deleted: category {categoryId}, by user {userId}");
    }

    public async Task<CategoryRepairResult> RepairPathsAsync(CancellationToken ct) => new(await store.RepairPathsAsync(ct));

    private static void CheckLimits(CategoryFormDto f)
    {
        Length("Назва", f.Name, 255);
        Length("Назва товару", f.ProductName, 255);
        Length("Рекламна назва товару", f.AdvertisingProductName, 255);
        Length("Опис", f.Description, 65_000);
        Length("Опис таблиці розмірів", f.DescriptionTableSize, 65_000);
        Length("Meta title", f.MetaTitle, 255);
        Length("Meta description", f.MetaDescription, 255);
        Length("Meta keywords", f.MetaKeyword, 255);
        Length("SEO-адреса", f.SeoKeyword, 255);
        Length("Картинка", f.Image, 255);
        Length("Картинка таблиці розмірів", f.ImageTableSize, 255);
        Range("Колонки", f.Column, 0, 10);
        Range("Строк виготовлення", f.ProductionPeriod, 0, 365);
        Count("фільтрів", f.FilterIds, MaxFilters);
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex Color();
}
