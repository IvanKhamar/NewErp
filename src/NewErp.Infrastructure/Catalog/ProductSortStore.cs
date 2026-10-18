using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;
using NewErp.Domain.Catalog;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Catalog;

public sealed class ProductSortStore(ErpDbContext db, IOptions<ErpOptions> erpOptions, IOptions<ProductSortOptions> sortOptions, TimeProvider clock)
    : IProductSortStore
{
    private const string CrossSellMetricKey = "crosssell_sort_metric";
    private const int NoCrossSellPosition = 999999;

    private readonly int _languageId = erpOptions.Value.LanguageId;
    private readonly ProductSortOptions _options = sortOptions.Value;

    public async Task<IReadOnlyList<SortGroup>> ListGroupsAsync(CancellationToken ct)
    {
        var groups = new List<SortGroup>();

        var tags = await (
            from f in db.DdFilters
            join fd in db.DdFilterDescriptions on f.FilterId equals fd.FilterId
            where f.FilterGroupId == _options.TagFilterGroupId && fd.LanguageId == _languageId
            orderby f.FilterId
            select new { f.FilterId, fd.Name }
        ).ToListAsync(ct);
        groups.AddRange(tags.Select(t => new SortGroup(SortGroupType.Tag, t.Name, "За темою: " + TagTitle(t.Name), FilterId: t.FilterId)));

        // Лише категорії, де є хоч один увімкнений товар. Категорія з товарами в кольорах ділиться на групи за кольорами.
        var categoryIds = await (
            from pc in db.DdProductToCategories
            join p in db.DdProducts on pc.ProductId equals p.ProductId
            where p.Status
            select pc.CategoryId
        ).Distinct().OrderBy(id => id).ToListAsync(ct);
        var colorIds = _options.ColorFilterIds;
        var colors = (await (
                from pc in db.DdProductToCategories
                join p in db.DdProducts on pc.ProductId equals p.ProductId
                join pf in db.DdProductFilters on pc.ProductId equals pf.ProductId
                join fd in db.DdFilterDescriptions on pf.FilterId equals fd.FilterId
                where p.Status && colorIds.Contains(pf.FilterId) && fd.LanguageId == _languageId
                select new { pc.CategoryId, pf.FilterId, fd.Name }
            ).Distinct().ToListAsync(ct))
            .OrderBy(c => c.FilterId)
            .ToLookup(c => c.CategoryId);
        var categoryNames = await db.DdCategoryDescriptions.AsNoTracking()
            .Where(c => c.LanguageId == _languageId && categoryIds.Contains(c.CategoryId))
            .ToDictionaryAsync(c => c.CategoryId, c => c.Name, ct);
        foreach (var categoryId in categoryIds)
        {
            var name = categoryNames.GetValueOrDefault(categoryId) ?? $"Категорія #{categoryId}";
            if (colors[categoryId].Any())
                groups.AddRange(colors[categoryId].Select(c =>
                    new SortGroup(SortGroupType.CategoryColor, $"{categoryId}_{c.FilterId}", $"{name} — {c.Name}", categoryId, c.FilterId)));
            else
                groups.Add(new SortGroup(SortGroupType.Category, categoryId.ToString(CultureInfo.InvariantCulture), name, categoryId));
        }

        var labels = await db.DdProductLabels.AsNoTracking()
            .Where(l => l.Status == true)
            .OrderBy(l => l.LabelId)
            .Select(l => new { l.LabelId, l.Name })
            .ToListAsync(ct);
        groups.AddRange(labels.Select(l => new SortGroup(SortGroupType.Label, l.Name, "Мітка: " + l.Name, LabelId: l.LabelId)));

        groups.Add(new SortGroup(SortGroupType.CrossSell, SortGroup.CrossSellKey, "Допродаж (оформлення)"));
        return groups;
    }

    public async Task<IReadOnlyList<int>> GroupProductIdsAsync(SortGroup group, CancellationToken ct)
    {
        // group — ключове слово в LINQ-запитах, тому значення беремо в окремі змінні.
        var (categoryId, filterId, labelId) = (group.CategoryId, group.FilterId, group.LabelId);
        var enabled = db.DdProducts.Where(p => p.Status);
        IQueryable<int> ids = group.Type switch
        {
            SortGroupType.Category or SortGroupType.CategoryColor =>
                from p in enabled
                where db.DdProductToCategories.Any(c => c.ProductId == p.ProductId && c.CategoryId == categoryId)
                    && (filterId == null || db.DdProductFilters.Any(f => f.ProductId == p.ProductId && f.FilterId == filterId))
                orderby p.ProductId
                select p.ProductId,
            SortGroupType.Tag =>
                from p in enabled
                where db.DdProductFilters.Any(f => f.ProductId == p.ProductId && f.FilterId == filterId)
                orderby p.ProductId
                select p.ProductId,
            SortGroupType.Label =>
                from p in enabled
                where db.DdProductToLabels.Any(l => l.ProductId == p.ProductId && l.LabelId == labelId)
                orderby p.DateAdded descending, p.ProductId
                select p.ProductId,
            SortGroupType.CrossSell => CrossSellProducts(),
            _ => throw new ArgumentOutOfRangeException(nameof(group)),
        };
        return await ids.ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, int>> SalesAsync(
        IReadOnlyCollection<int> productIds, DateTime from, DateTime to, bool crossSellOnly, CancellationToken ct)
    {
        var (start, end) = (from, to);
        var statuses = _options.CountedStatusIds;
        var excludedUsers = _options.ExcludedUserIds;
        var lines =
            from op in db.DdOrderProducts
            join o in db.DdOrders on op.OrderId equals o.OrderId
            where productIds.Contains(op.ProductId) && o.DateAdded >= start && o.DateAdded <= end
                && statuses.Contains(o.OrderStatusId) && !excludedUsers.Contains(o.UserId)
                && (!crossSellOnly || op.Crosssell)
            select new { op.ProductId, op.Quantity, o.Firstname, o.Lastname, o.PaymentCompany, o.Comment };
        // Оптові й партнерські замовлення не показують, що купують у магазині (LIKE у MySQL без урахування регістру).
        foreach (var marker in _options.ExcludedOrderMarkers)
            lines = lines.Where(l => !l.Firstname.Contains(marker) && !l.Lastname.Contains(marker)
                && !l.PaymentCompany.Contains(marker) && !l.Comment.Contains(marker));
        foreach (var marker in _options.ExcludedCommentMarkers)
            lines = lines.Where(l => !l.Comment.Contains(marker));

        return await lines.GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Sales = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(s => s.ProductId, s => s.Sales, ct);
    }

    public async Task<IReadOnlyList<SortProductDto>> ProductsAsync(IReadOnlyCollection<int> productIds, bool crossSell, CancellationToken ct) =>
        await (
            from p in db.DdProducts
            where productIds.Contains(p.ProductId)
            join pd in db.DdProductDescriptions.Where(d => d.LanguageId == _languageId) on p.ProductId equals pd.ProductId into names
            from pd in names.DefaultIfEmpty()
            join cs in db.DdCrosssellSorts on p.ProductId equals cs.ProductId into panel
            from cs in panel.DefaultIfEmpty()
            select new SortProductDto(p.ProductId, pd == null ? "" : pd.Name, p.Image ?? "",
                crossSell ? (cs == null ? NoCrossSellPosition : cs.SortOrder) : p.SortOrder)
        ).ToListAsync(ct);

    public async Task<int> ApplyAsync(bool crossSell, IReadOnlyList<int> orderedProductIds, CancellationToken ct)
    {
        if (crossSell)
        {
            // Склад панелі міг змінитися, тож її порядок перезаписується повністю, як на старому сайті.
            await db.DdCrosssellSorts.ExecuteDeleteAsync(ct);
            db.DdCrosssellSorts.AddRange(orderedProductIds.Select((id, index) => new DdCrosssellSort { ProductId = id, SortOrder = index + 1 }));
            await db.SaveChangesAsync(ct);
            return orderedProductIds.Count;
        }

        // Одним UPDATE … CASE на кілька сотень товарів замість окремого запиту на кожен. У SQL ідуть лише числа.
        foreach (var chunk in orderedProductIds.Select((id, index) => (id, position: index + 1)).Chunk(500))
        {
            var sql = new StringBuilder("UPDATE dd_product SET sort_order = CASE product_id");
            foreach (var (id, position) in chunk)
                sql.Append(CultureInfo.InvariantCulture, $" WHEN {id} THEN {position}");
            sql.Append(" END WHERE product_id IN (").AppendJoin(',', chunk.Select(c => c.id)).Append(')');
            await db.Database.ExecuteSqlRawAsync(sql.ToString(), ct);
        }
        return orderedProductIds.Count;
    }

    public async Task<IReadOnlyList<SortPin>> PinsAsync(SortGroup group, CancellationToken ct) =>
        await db.DdProductManualPins.AsNoTracking()
            .Where(p => p.GroupType == group.TypeCode && p.GroupKey == group.Key)
            .OrderBy(p => p.Position).ThenBy(p => p.Id)
            .Select(p => new SortPin(p.Position, p.ProductId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SortPinDto>> ListPinsAsync(CancellationToken ct)
    {
        var pins = await (
            from pin in db.DdProductManualPins
            join pd in db.DdProductDescriptions.Where(d => d.LanguageId == _languageId) on pin.ProductId equals pd.ProductId into names
            from pd in names.DefaultIfEmpty()
            orderby pin.GroupType, pin.GroupKey, pin.Position
            select new { pin.Id, pin.GroupType, pin.GroupKey, pin.Position, pin.ProductId, Name = pd == null ? "" : pd.Name }
        ).ToListAsync(ct);
        var known = Enum.GetValues<SortGroupType>().ToDictionary(SortGroup.TypeToCode);
        return pins.Where(p => known.ContainsKey(p.GroupType))
            .Select(p => new SortPinDto(p.Id, known[p.GroupType], p.GroupKey, p.Position, p.ProductId, p.Name))
            .ToList();
    }

    public async Task SetPinAsync(SortGroup group, int position, int productId, DateTime now, CancellationToken ct)
    {
        await db.DdProductManualPins
            .Where(p => p.GroupType == group.TypeCode && p.GroupKey == group.Key && (p.ProductId == productId || p.Position == position))
            .ExecuteDeleteAsync(ct);
        db.DdProductManualPins.Add(new DdProductManualPin
        {
            GroupType = group.TypeCode, GroupKey = group.Key, Position = position, ProductId = productId, CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> RemovePinAsync(int id, CancellationToken ct) =>
        await db.DdProductManualPins.Where(p => p.Id == id).ExecuteDeleteAsync(ct) > 0;

    public async Task<int> CreateSnapshotAsync(string note, DateTime now, CancellationToken ct)
    {
        var snapshot = new DdProductOrderSnapshot { Note = note.Length > 255 ? note[..255] : note, CreatedAt = now };
        db.DdProductOrderSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dd_product_order_snapshot_item (snapshot_id, product_id, sort_order) SELECT {snapshot.SnapshotId}, product_id, sort_order FROM dd_product",
            ct);
        return snapshot.SnapshotId;
    }

    public async Task<IReadOnlyList<SortSnapshotDto>> ListSnapshotsAsync(int limit, CancellationToken ct) =>
        await db.DdProductOrderSnapshots.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.SnapshotId)
            .Take(limit)
            .Select(s => new SortSnapshotDto(s.SnapshotId, s.Note, s.CreatedAt))
            .ToListAsync(ct);

    public async Task<bool> RestoreSnapshotAsync(int id, CancellationToken ct)
    {
        if (!await db.DdProductOrderSnapshots.AnyAsync(s => s.SnapshotId == id, ct))
            return false;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dd_product p JOIN dd_product_order_snapshot_item s ON s.product_id = p.product_id SET p.sort_order = s.sort_order WHERE s.snapshot_id = {id}",
            ct);
        return true;
    }

    public Task<bool> CrossSellPanelOnlyAsync(CancellationToken ct) =>
        db.DdSettings.AnyAsync(s => s.StoreId == 0 && s.Key == CrossSellMetricKey && s.Value == "panel", ct);

    public async Task SetCrossSellPanelOnlyAsync(bool panelOnly, CancellationToken ct)
    {
        await db.DdSettings.Where(s => s.StoreId == 0 && s.Key == CrossSellMetricKey).ExecuteDeleteAsync(ct);
        db.DdSettings.Add(new DdSetting
        {
            StoreId = 0, Code = "crosssell_sort", Key = CrossSellMetricKey, Value = panelOnly ? "panel" : "all", Serialized = false,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SortProductDto>> SearchProductsAsync(string term, int limit, CancellationToken ct) =>
        await (
            from p in db.DdProducts
            join pd in db.DdProductDescriptions on p.ProductId equals pd.ProductId
            where p.Status && pd.LanguageId == _languageId && pd.Name.Contains(term)
            orderby pd.Name
            select new SortProductDto(p.ProductId, pd.Name, p.Image ?? "", p.SortOrder)
        ).Take(limit).ToListAsync(ct);

    /// <summary>
    /// Склад панелі допродажу, як на вітрині: окремі товари й товари обраних категорій, увімкнені, у магазині 0 і вже доступні,
    /// за поточним порядком товару.
    /// </summary>
    private IQueryable<int> CrossSellProducts()
    {
        var now = clock.GetLocalNow().DateTime;
        return
            from p in db.DdProducts
            where p.Status && p.DateAvailable <= now
                && db.DdProductToStores.Any(s => s.ProductId == p.ProductId && s.StoreId == 0)
                && (db.DdCrosssellItems.Any(i => i.Type == "product" && i.TargetId == p.ProductId)
                    || (from pc in db.DdProductToCategories
                        join i in db.DdCrosssellItems on pc.CategoryId equals i.TargetId
                        where i.Type == "category" && pc.ProductId == p.ProductId
                        select pc).Any())
            orderby p.SortOrder, p.ProductId
            select p.ProductId;
    }

    // Як ucfirst(str_replace(':', ' ', назва)) у старому коді.
    private static string TagTitle(string name)
    {
        var title = name.Replace(':', ' ');
        return title.Length == 0 ? title : char.ToUpper(title[0], CultureInfo.CurrentCulture) + title[1..];
    }
}
