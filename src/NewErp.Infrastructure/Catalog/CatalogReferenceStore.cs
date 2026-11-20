using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Catalog;

/// <summary>
/// Фільтри, атрибути, опції, виробники й завантаження над таблицями OpenCart (пункт 49). Три дворівневі довідники різняться
/// лише таблицями: група з назвами мовами й значення з назвами мовами.
/// </summary>
public sealed class CatalogReferenceStore(ErpDbContext db, IOptions<ErpOptions> options, TimeProvider clock) : ICatalogReferenceStore
{
    private const int StoreId = 0;
    private static readonly StringComparer Names = StringComparer.Create(CultureInfo.GetCultureInfo("uk-UA"), ignoreCase: true);

    private readonly int _languageId = options.Value.LanguageId;

    private sealed record GroupRow(int Id, string Name, int SortOrder, string Type);

    private sealed record ItemRow(int Id, int GroupId, string Name, int SortOrder, string Image, int Products);

    // ═══ Дворівневі довідники ═══

    public async Task<IReadOnlyList<ReferenceGroupDto>> GroupsAsync(ReferenceKind kind, CancellationToken ct)
    {
        var (groups, items) = kind switch
        {
            ReferenceKind.Filters => (await FilterGroupsAsync(ct), await FiltersAsync(ct)),
            ReferenceKind.Attributes => (await AttributeGroupsAsync(ct), await AttributesAsync(ct)),
            _ => (await OptionGroupsAsync(ct), await OptionValuesAsync(ct)),
        };
        var byGroup = items.OrderBy(i => i.SortOrder).ThenBy(i => i.Name, Names).ToLookup(i => i.GroupId);
        return [.. groups.OrderBy(g => g.SortOrder).ThenBy(g => g.Name, Names)
            .Select(g => new ReferenceGroupDto(g.Id, g.Name, g.SortOrder, g.Type,
                [.. byGroup[g.Id].Select(i => new ReferenceItemDto(i.Id, i.Name, i.SortOrder, i.Image, i.Products))]))];
    }

    private async Task<List<GroupRow>> FilterGroupsAsync(CancellationToken ct) => await (
        from g in db.FilterGroups.AsNoTracking()
        join n in db.FilterGroupDescriptions.Where(x => x.LanguageId == _languageId) on g.Id equals n.FilterGroupId into ns
        from n in ns.DefaultIfEmpty()
        select new GroupRow(g.Id, n == null ? "" : n.Name, g.SortOrder, "select")).ToListAsync(ct);

    private async Task<List<ItemRow>> FiltersAsync(CancellationToken ct) => await (
        from f in db.DdFilters.AsNoTracking()
        join n in db.DdFilterDescriptions.Where(x => x.LanguageId == _languageId) on f.FilterId equals n.FilterId into ns
        from n in ns.DefaultIfEmpty()
        select new ItemRow(f.FilterId, f.FilterGroupId, n == null ? "" : n.Name, f.SortOrder, "",
            db.DdProductFilters.Count(p => p.FilterId == f.FilterId) + db.CategoryFilters.Count(c => c.FilterId == f.FilterId))).ToListAsync(ct);

    private async Task<List<GroupRow>> AttributeGroupsAsync(CancellationToken ct) => await (
        from g in db.AttributeGroups.AsNoTracking()
        join n in db.CatalogAttributeGroupDescriptions.Where(x => x.LanguageId == _languageId) on g.Id equals n.AttributeGroupId into ns
        from n in ns.DefaultIfEmpty()
        select new GroupRow(g.Id, n == null ? "" : n.Name, g.SortOrder, "select")).ToListAsync(ct);

    private async Task<List<ItemRow>> AttributesAsync(CancellationToken ct) => await (
        from a in db.CatalogAttributes.AsNoTracking()
        join n in db.CatalogAttributeDescriptions.Where(x => x.LanguageId == _languageId) on a.AttributeId equals n.AttributeId into ns
        from n in ns.DefaultIfEmpty()
        select new ItemRow(a.AttributeId, a.AttributeGroupId, n == null ? "" : n.Name, a.SortOrder, "",
            db.ProductAttributes.Count(p => p.AttributeId == a.AttributeId))).ToListAsync(ct);

    private async Task<List<GroupRow>> OptionGroupsAsync(CancellationToken ct) => await (
        from o in db.CatalogOptions.AsNoTracking()
        join n in db.DdOptionDescriptions.Where(x => x.LanguageId == _languageId) on o.OptionId equals n.OptionId into ns
        from n in ns.DefaultIfEmpty()
        select new GroupRow(o.OptionId, n == null ? "" : n.Name, o.SortOrder, o.Type)).ToListAsync(ct);

    private async Task<List<ItemRow>> OptionValuesAsync(CancellationToken ct) => await (
        from v in db.CatalogOptionValues.AsNoTracking()
        join n in db.DdOptionValueDescriptions.Where(x => x.LanguageId == _languageId) on v.OptionValueId equals n.OptionValueId into ns
        from n in ns.DefaultIfEmpty()
        select new ItemRow(v.OptionValueId, v.OptionId, n == null ? "" : n.Name, v.SortOrder, v.Image,
            db.DdProductOptionValues.Count(p => p.OptionValueId == v.OptionValueId))).ToListAsync(ct);

    public async Task<int> SaveGroupAsync(ReferenceKind kind, ReferenceGroupDto group, CancellationToken ct)
    {
        int id;
        switch (kind)
        {
            case ReferenceKind.Filters:
                var filterGroup = group.Id == 0 ? db.FilterGroups.Add(new FilterGroup()).Entity : await db.FilterGroups.SingleAsync(g => g.Id == group.Id, ct);
                filterGroup.SortOrder = group.SortOrder;
                await db.SaveChangesAsync(ct);
                id = filterGroup.Id;
                await db.FilterGroupDescriptions.Where(d => d.FilterGroupId == id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
                db.FilterGroupDescriptions.Add(new FilterGroupDescription { FilterGroupId = id, LanguageId = _languageId, Name = group.Name });
                break;
            case ReferenceKind.Attributes:
                var attributeGroup = group.Id == 0 ? db.AttributeGroups.Add(new AttributeGroup()).Entity : await db.AttributeGroups.SingleAsync(g => g.Id == group.Id, ct);
                attributeGroup.SortOrder = group.SortOrder;
                await db.SaveChangesAsync(ct);
                id = attributeGroup.Id;
                await db.CatalogAttributeGroupDescriptions.Where(d => d.AttributeGroupId == id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
                db.CatalogAttributeGroupDescriptions.Add(new CatalogAttributeGroupDescription { AttributeGroupId = id, LanguageId = _languageId, Name = group.Name });
                break;
            default:
                var option = group.Id == 0 ? db.CatalogOptions.Add(new CatalogOption()).Entity : await db.CatalogOptions.SingleAsync(o => o.OptionId == group.Id, ct);
                (option.SortOrder, option.Type) = (group.SortOrder, group.Type);
                await db.SaveChangesAsync(ct);
                id = option.OptionId;
                await db.DdOptionDescriptions.Where(d => d.OptionId == id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
                db.DdOptionDescriptions.Add(new DdOptionDescription { OptionId = id, LanguageId = _languageId, Name = group.Name });
                break;
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return id;
    }

    public async Task<int> SaveItemAsync(ReferenceKind kind, int groupId, ReferenceItemDto item, CancellationToken ct)
    {
        int id;
        switch (kind)
        {
            case ReferenceKind.Filters:
                var filter = item.Id == 0 ? db.DdFilters.Add(new DdFilter()).Entity : await db.DdFilters.SingleAsync(f => f.FilterId == item.Id, ct);
                (filter.FilterGroupId, filter.SortOrder) = (groupId, item.SortOrder);
                await db.SaveChangesAsync(ct);
                id = filter.FilterId;
                await db.DdFilterDescriptions.Where(d => d.FilterId == id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
                db.DdFilterDescriptions.Add(new DdFilterDescription { FilterId = id, LanguageId = _languageId, FilterGroupId = groupId, Name = item.Name });
                break;
            case ReferenceKind.Attributes:
                var attribute = item.Id == 0 ? db.CatalogAttributes.Add(new CatalogAttribute()).Entity : await db.CatalogAttributes.SingleAsync(a => a.AttributeId == item.Id, ct);
                (attribute.AttributeGroupId, attribute.SortOrder) = (groupId, item.SortOrder);
                await db.SaveChangesAsync(ct);
                id = attribute.AttributeId;
                await db.CatalogAttributeDescriptions.Where(d => d.AttributeId == id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
                db.CatalogAttributeDescriptions.Add(new CatalogAttributeDescription { AttributeId = id, LanguageId = _languageId, Name = item.Name });
                break;
            default:
                var value = item.Id == 0 ? db.CatalogOptionValues.Add(new CatalogOptionValue()).Entity : await db.CatalogOptionValues.SingleAsync(v => v.OptionValueId == item.Id, ct);
                (value.OptionId, value.SortOrder, value.Image) = (groupId, item.SortOrder, item.Image);
                await db.SaveChangesAsync(ct);
                id = value.OptionValueId;
                await db.DdOptionValueDescriptions.Where(d => d.OptionValueId == id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
                db.DdOptionValueDescriptions.Add(new DdOptionValueDescription { OptionValueId = id, LanguageId = _languageId, OptionId = groupId, Name = item.Name });
                break;
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return id;
    }

    public async Task<int?> GroupOfAsync(ReferenceKind kind, int itemId, CancellationToken ct) => kind switch
    {
        ReferenceKind.Filters => await db.DdFilters.Where(f => f.FilterId == itemId).Select(f => (int?)f.FilterGroupId).FirstOrDefaultAsync(ct),
        ReferenceKind.Attributes => await db.CatalogAttributes.Where(a => a.AttributeId == itemId).Select(a => (int?)a.AttributeGroupId).FirstOrDefaultAsync(ct),
        _ => await db.CatalogOptionValues.Where(v => v.OptionValueId == itemId).Select(v => (int?)v.OptionId).FirstOrDefaultAsync(ct),
    };

    public async Task DeleteGroupAsync(ReferenceKind kind, int groupId, CancellationToken ct)
    {
        switch (kind)
        {
            case ReferenceKind.Filters:
                var filters = await db.DdFilters.Where(f => f.FilterGroupId == groupId).Select(f => f.FilterId).ToListAsync(ct);
                await db.DdFilterDescriptions.Where(d => filters.Contains(d.FilterId)).ExecuteDeleteAsync(ct);
                await db.DdFilters.Where(f => f.FilterGroupId == groupId).ExecuteDeleteAsync(ct);
                await db.FilterGroupDescriptions.Where(d => d.FilterGroupId == groupId).ExecuteDeleteAsync(ct);
                await db.FilterGroups.Where(g => g.Id == groupId).ExecuteDeleteAsync(ct);
                break;
            case ReferenceKind.Attributes:
                var attributes = await db.CatalogAttributes.Where(a => a.AttributeGroupId == groupId).Select(a => a.AttributeId).ToListAsync(ct);
                await db.CatalogAttributeDescriptions.Where(d => attributes.Contains(d.AttributeId)).ExecuteDeleteAsync(ct);
                await db.CatalogAttributes.Where(a => a.AttributeGroupId == groupId).ExecuteDeleteAsync(ct);
                await db.CatalogAttributeGroupDescriptions.Where(d => d.AttributeGroupId == groupId).ExecuteDeleteAsync(ct);
                await db.AttributeGroups.Where(g => g.Id == groupId).ExecuteDeleteAsync(ct);
                break;
            default:
                var values = await db.CatalogOptionValues.Where(v => v.OptionId == groupId).Select(v => v.OptionValueId).ToListAsync(ct);
                await db.DdOptionValueDescriptions.Where(d => values.Contains(d.OptionValueId)).ExecuteDeleteAsync(ct);
                await db.CatalogOptionValues.Where(v => v.OptionId == groupId).ExecuteDeleteAsync(ct);
                await db.DdOptionDescriptions.Where(d => d.OptionId == groupId).ExecuteDeleteAsync(ct);
                await db.CatalogOptions.Where(o => o.OptionId == groupId).ExecuteDeleteAsync(ct);
                // Жоден товар не має значення цієї опції (перевірено), тож лишилися хіба порожні зв'язки товарів з нею.
                await db.DdProductOptions.Where(p => p.OptionId == groupId).ExecuteDeleteAsync(ct);
                break;
        }
    }

    public async Task DeleteItemAsync(ReferenceKind kind, int itemId, CancellationToken ct)
    {
        switch (kind)
        {
            case ReferenceKind.Filters:
                await db.DdFilterDescriptions.Where(d => d.FilterId == itemId).ExecuteDeleteAsync(ct);
                await db.DdFilters.Where(f => f.FilterId == itemId).ExecuteDeleteAsync(ct);
                break;
            case ReferenceKind.Attributes:
                await db.CatalogAttributeDescriptions.Where(d => d.AttributeId == itemId).ExecuteDeleteAsync(ct);
                await db.CatalogAttributes.Where(a => a.AttributeId == itemId).ExecuteDeleteAsync(ct);
                break;
            default:
                await db.DdOptionValueDescriptions.Where(d => d.OptionValueId == itemId).ExecuteDeleteAsync(ct);
                await db.CatalogOptionValues.Where(v => v.OptionValueId == itemId).ExecuteDeleteAsync(ct);
                break;
        }
    }

    // ═══ Виробники ═══

    public async Task<IReadOnlyList<ManufacturerDto>> ManufacturersAsync(CancellationToken ct) =>
        await db.Manufacturers.AsNoTracking().OrderBy(m => m.SortOrder).ThenBy(m => m.Name)
            .Select(m => new ManufacturerDto(m.Id, m.Name, m.Image ?? "", m.SortOrder, db.DdProducts.Count(p => p.ManufacturerId == m.Id)))
            .ToListAsync(ct);

    public async Task<int> SaveManufacturerAsync(ManufacturerDto manufacturer, CancellationToken ct)
    {
        var row = manufacturer.Id == 0 ? db.Manufacturers.Add(new Manufacturer()).Entity : await db.Manufacturers.SingleAsync(m => m.Id == manufacturer.Id, ct);
        (row.Name, row.Image, row.SortOrder) = (manufacturer.Name, manufacturer.Image.Length > 0 ? manufacturer.Image : null, manufacturer.SortOrder);
        await db.SaveChangesAsync(ct);
        if (manufacturer.Id == 0)
        {
            db.ManufacturerToStores.Add(new ManufacturerToStore { ManufacturerId = row.Id, StoreId = StoreId });
            await db.SaveChangesAsync(ct);
        }
        db.ChangeTracker.Clear();
        return row.Id;
    }

    public async Task DeleteManufacturerAsync(int manufacturerId, CancellationToken ct)
    {
        await db.ManufacturerToStores.Where(m => m.ManufacturerId == manufacturerId).ExecuteDeleteAsync(ct);
        await db.Manufacturers.Where(m => m.Id == manufacturerId).ExecuteDeleteAsync(ct);
        var query = $"manufacturer_id={manufacturerId}";
        await db.SeoUrls.Where(s => s.Query == query).ExecuteDeleteAsync(ct);
    }

    // ═══ Завантаження ═══

    public async Task<IReadOnlyList<DownloadDto>> DownloadsAsync(CancellationToken ct) => await (
        from d in db.Downloads.AsNoTracking()
        join n in db.DownloadDescriptions.Where(x => x.LanguageId == _languageId) on d.Id equals n.DownloadId into ns
        from n in ns.DefaultIfEmpty()
        orderby n == null ? "" : n.Name
        select new DownloadDto(d.Id, n == null ? "" : n.Name, d.Filename, d.Mask, db.ProductToDownloads.Count(p => p.DownloadId == d.Id)))
        .ToListAsync(ct);

    public async Task<int> SaveDownloadAsync(DownloadDto download, CancellationToken ct)
    {
        var row = download.Id == 0 ? db.Downloads.Add(new Download { DateAdded = Now() }).Entity : await db.Downloads.SingleAsync(d => d.Id == download.Id, ct);
        (row.Filename, row.Mask) = (download.Filename, download.Mask);
        await db.SaveChangesAsync(ct);
        await db.DownloadDescriptions.Where(d => d.DownloadId == row.Id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
        db.DownloadDescriptions.Add(new DownloadDescription { DownloadId = row.Id, LanguageId = _languageId, Name = download.Name });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return row.Id;
    }

    public async Task DeleteDownloadAsync(int downloadId, CancellationToken ct)
    {
        await db.DownloadDescriptions.Where(d => d.DownloadId == downloadId).ExecuteDeleteAsync(ct);
        await db.Downloads.Where(d => d.Id == downloadId).ExecuteDeleteAsync(ct);
    }

    private DateTime Now()
    {
        var now = clock.GetLocalNow().DateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}
