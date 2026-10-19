using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Production;
using NewErp.Domain.Production;
using NewErp.Domain.Stock;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Production;

public sealed class ProductionRepository(ErpDbContext db, IOptions<ErpOptions> options) : IProductionRepository, ISewingPlanSource
{
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<IReadOnlyList<OrderSizeLine>> GetOrderSizeLinesAsync(int orderId, CancellationToken ct) =>
        (await GetSizeLinesAsync([orderId], ct)).Select(l => l.Line).ToList();

    /// <summary>Рядки кількох замовлень з моделлю виробництва й опціями (для плану пошиву).</summary>
    private async Task<List<(int OrderId, OrderSizeLine Line)>> GetSizeLinesAsync(IReadOnlyCollection<int> orderIds, CancellationToken ct)
    {
        var products = await db.DdOrderProducts.AsNoTracking()
            .Where(p => orderIds.Contains(p.OrderId))
            .OrderBy(p => p.OrderId).ThenBy(p => p.OrderProductId)
            .Select(p => new { p.OrderId, p.OrderProductId, p.ProductId, p.Name, p.Quantity })
            .ToListAsync(ct);
        if (products.Count == 0)
            return [];
        var productIds = products.Select(p => p.ProductId).Distinct().ToList();

        // Опція в замовленні посилається на product_option_value, а розмір моделі — на option_value_id із нього.
        var options = await (
            from o in db.DdOrderOptions
            join v in db.DdProductOptionValues on o.ProductOptionValueId equals v.ProductOptionValueId
            where orderIds.Contains(o.OrderId)
            select new { o.OrderProductId, v.OptionValueId }
        ).ToListAsync(ct);

        var direct = await (
            from l in db.Set<ProductionProductLink>()
            join m in db.ProductionModels on l.ProductionId equals m.Id
            where m.IsActive && productIds.Contains(l.ProductId)
            select new { l.ProductId, l.ProductionId }
        ).ToListAsync(ct);

        // Прив'язка через категорію: порожній фільтр — уся категорія, інакше назва товару містить фільтр
        // (або дорівнює йому, якщо match_exact). Як у старому модулі, порівняння без урахування регістру (collation бази).
        var byCategory = await (
            from c in db.Set<ProductionCategoryLink>()
            join m in db.ProductionModels on c.ProductionId equals m.Id
            join pc in db.DdProductToCategories on c.CategoryId equals pc.CategoryId
            join pd in db.DdProductDescriptions on pc.ProductId equals pd.ProductId
            where m.IsActive && pd.LanguageId == _languageId && productIds.Contains(pc.ProductId)
                && (c.NameFilter == ""
                    || (c.MatchExact && pd.Name == c.NameFilter)
                    || (!c.MatchExact && pd.Name.Contains(c.NameFilter)))
            select new { pc.ProductId, c.ProductionId, HasFilter = c.NameFilter != "" }
        ).ToListAsync(ct);

        // Пряма прив'язка важливіша за категорію, а категорія з фільтром — за всю категорію.
        int? ProductionFor(int productId) =>
            direct.Where(d => d.ProductId == productId).Select(d => (int?)d.ProductionId).Min()
            ?? byCategory.Where(c => c.ProductId == productId)
                .OrderByDescending(c => c.HasFilter).ThenBy(c => c.ProductionId)
                .Select(c => (int?)c.ProductionId).FirstOrDefault();

        var optionsByLine = options.ToLookup(o => o.OrderProductId, o => o.OptionValueId);
        return products
            .Select(p => (p.OrderId, new OrderSizeLine(
                p.ProductId, p.Name, p.Quantity, ProductionFor(p.ProductId), optionsByLine[p.OrderProductId].ToList())))
            .ToList();
    }

    public async Task<IReadOnlyList<PlanOrder>> GetOrdersAsync(IReadOnlyCollection<int> statusIds, CancellationToken ct)
    {
        if (statusIds.Count == 0)
            return [];
        var orders = await db.DdOrders.AsNoTracking()
            .Where(o => statusIds.Contains(o.OrderStatusId))
            .OrderBy(o => o.OrderId)
            .Select(o => new { o.OrderId, o.DateAdded })
            .ToListAsync(ct);
        if (orders.Count == 0)
            return [];

        var ids = orders.Select(o => o.OrderId).ToList();
        var lines = (await GetSizeLinesAsync(ids, ct)).ToLookup(l => l.OrderId, l => l.Line);
        var held = await GetSewnHeldAsync(ids, ct);
        return orders
            .Select(o => new PlanOrder(
                o.OrderId, o.DateAdded, lines[o.OrderId].ToList(),
                held.GetValueOrDefault(o.OrderId) ?? new Dictionary<int, decimal>()))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<int, int>> GetProductionDaysAsync(IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        var rows = await (
            from pc in db.DdProductToCategories
            join c in db.DdCategories on pc.CategoryId equals c.CategoryId
            where productIds.Contains(pc.ProductId)
            select new { pc.ProductId, pc.CategoryId, c.ProductionPeriod }
        ).ToListAsync(ct);
        return rows
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.MinBy(r => r.CategoryId)!.ProductionPeriod);
    }

    public Task<IReadOnlyList<ProductionModel>> GetModelsAsync(IReadOnlyCollection<int> productionIds, CancellationToken ct) =>
        FindModelsAsync(productionIds, ct);

    /// <summary>Те саме, що <see cref="GetSewnHeldByOrderAsync"/>, але для багатьох замовлень одним запитом.</summary>
    private async Task<Dictionary<int, IReadOnlyDictionary<int, decimal>>> GetSewnHeldAsync(IReadOnlyCollection<int> orderIds, CancellationToken ct)
    {
        var byComment = orderIds
            .SelectMany(id => new[] { (Comment: ProductionModel.OrderComment(id), OrderId: id, Taken: true), (Comment: ProductionModel.CancelComment(id), OrderId: id, Taken: false) })
            .ToDictionary(x => x.Comment, x => (x.OrderId, x.Taken));
        var comments = byComment.Keys.ToList();
        var rows = await db.ProductionLog.AsNoTracking()
            .Where(l => l.Stage == ProductionLogEntry.Sewn && comments.Contains(l.Comment))
            .Select(l => new { l.SizeId, l.Direction, l.Comment, l.Quantity })
            .ToListAsync(ct);

        return rows
            .Select(r => (Row: r, Key: byComment[r.Comment]))
            .Where(x => x.Key.Taken ? x.Row.Direction == ProductionLogEntry.Out : x.Row.Direction == ProductionLogEntry.In)
            .GroupBy(x => x.Key.OrderId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<int, decimal>)g.GroupBy(x => x.Row.SizeId)
                    .ToDictionary(s => s.Key, s => s.Sum(x => x.Key.Taken ? x.Row.Quantity : -x.Row.Quantity)));
    }

    public async Task<IReadOnlyList<ProductionModel>> FindModelsAsync(IReadOnlyCollection<int> productionIds, CancellationToken ct) =>
        await db.ProductionModels.Include(m => m.Sizes).Where(m => productionIds.Contains(m.Id)).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, decimal>> GetSewnHeldByOrderAsync(int orderId, CancellationToken ct)
    {
        var taken = ProductionModel.OrderComment(orderId);
        var returned = ProductionModel.CancelComment(orderId);
        var rows = await db.ProductionLog.AsNoTracking()
            .Where(l => l.Stage == ProductionLogEntry.Sewn && (l.Comment == taken || l.Comment == returned))
            .Select(l => new { l.SizeId, l.Direction, l.Comment, l.Quantity })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.SizeId)
            .ToDictionary(
                g => g.Key,
                g => g.Where(r => r.Comment == taken && r.Direction == ProductionLogEntry.Out).Sum(r => r.Quantity)
                   - g.Where(r => r.Comment == returned && r.Direction == ProductionLogEntry.In).Sum(r => r.Quantity));
    }
    public async Task<IReadOnlyList<ProductionModelSummaryDto>> ListAsync(ProductionModelFilter filter, CancellationToken ct)
    {
        var models = db.ProductionModels.AsNoTracking().Where(m => m.IsActive);
        if (!string.IsNullOrEmpty(filter.Type))
            models = models.Where(m => m.Type == filter.Type);

        var rows = await models
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Name)
            .Select(m => new
            {
                m.Id, m.Name, m.Type, m.MinTotal,
                Cut = m.Sizes.Sum(s => s.CutStock),
                Sewn = m.Sizes.Sum(s => s.SewnStock),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ProductionModelSummaryDto(r.Id, r.Name, r.Type, r.MinTotal, r.Cut, r.Sewn, Math.Max(0, r.MinTotal - r.Sewn)))
            .Where(r => !filter.BelowMinimumOnly || r.Deficit > 0)
            .ToList();
    }

    public async Task<ProductionModelDto?> GetDtoAsync(int productionId, CancellationToken ct)
    {
        var model = await LoadModel().AsNoTracking().FirstOrDefaultAsync(m => m.Id == productionId && m.IsActive, ct);
        if (model is null)
            return null;

        var components = await db.ProductionComponents.AsNoTracking().Include(c => c.Bom)
            .Where(c => c.ProductionId == productionId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(ct);
        var componentIds = components.Select(c => c.Id).ToList();
        var stock = await db.ComponentStocks.AsNoTracking().Where(s => componentIds.Contains(s.ComponentId)).ToListAsync(ct);

        var itemIds = components.SelectMany(c => c.Bom.Select(b => b.ItemId))
            .Concat(model.SewnBom.Where(b => b.ItemId != null).Select(b => b.ItemId!.Value))
            .Distinct().ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => (i.Name, i.Unit), ct);
        var componentNames = components.ToDictionary(c => c.Id, c => c.Name);

        return new ProductionModelDto(
            model.Id, model.Name, model.Type, model.MinTotal, model.SortOrder,
            model.Sizes.OrderBy(s => s.SortOrder)
                .Select(s => new ProductionSizeDto(s.Id, s.Name, s.Percent, s.CutStock, s.SewnStock, s.OptionValueId)).ToList(),
            model.ProductLinks.Select(l => l.ProductId).Order().ToList(),
            model.CategoryLinks.Select(l => new CategoryLinkDto(l.CategoryId, l.NameFilter, l.MatchExact)).ToList(),
            components.Select(c => new ComponentDto(
                c.Id, c.Name, c.SortOrder,
                stock.Where(s => s.ComponentId == c.Id).ToDictionary(s => s.SizeId, s => s.Stock),
                c.Bom.Select(b => new ComponentBomLineDto(
                    b.SizeId, b.ItemId, items.GetValueOrDefault(b.ItemId).Name, items.GetValueOrDefault(b.ItemId).Unit, b.QtyPerUnit)).ToList()))
                .ToList(),
            model.SewnBom.Select(b => new SewnBomLineDto(
                b.SizeId,
                b.ComponentId, b.ComponentId is { } cid ? componentNames.GetValueOrDefault(cid) : null,
                b.ItemId,
                b.ItemId is { } iid ? items.GetValueOrDefault(iid).Name : null,
                b.ItemId is { } uid ? items.GetValueOrDefault(uid).Unit : null,
                b.QtyPerUnit)).ToList());
    }

    public Task<ProductionModel?> FindModelAsync(int productionId, CancellationToken ct) =>
        LoadModel().FirstOrDefaultAsync(m => m.Id == productionId, ct);

    public Task<ProductionComponent?> FindComponentAsync(int componentId, CancellationToken ct) =>
        db.ProductionComponents.Include(c => c.Bom).FirstOrDefaultAsync(c => c.Id == componentId, ct);

    public async Task<IReadOnlyList<int>> ComponentIdsAsync(int productionId, CancellationToken ct) =>
        await db.ProductionComponents.Where(c => c.ProductionId == productionId).Select(c => c.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<int>> MissingStockItemIdsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct)
    {
        var existing = await db.StockItems.Where(i => i.IsActive && itemIds.Contains(i.Id)).Select(i => i.Id).ToListAsync(ct);
        return itemIds.Except(existing).ToList();
    }

    public async Task<bool> SizesInUseAsync(IReadOnlyCollection<int> sizeIds, CancellationToken ct) =>
        await db.ComponentBomLines.AnyAsync(b => b.SizeId != null && sizeIds.Contains(b.SizeId.Value), ct)
        || await db.ComponentStocks.AnyAsync(s => sizeIds.Contains(s.SizeId) && s.Stock != 0, ct);

    public async Task<IReadOnlyList<ComponentStock>> FindComponentStockAsync(int componentId, CancellationToken ct) =>
        await db.ComponentStocks.Where(s => s.ComponentId == componentId).ToListAsync(ct);

    public async Task<IReadOnlyList<ProductionComponent>> FindComponentsAsync(int productionId, CancellationToken ct) =>
        await db.ProductionComponents.Include(c => c.Bom)
            .Where(c => c.ProductionId == productionId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ComponentStock>> FindComponentStocksAsync(IReadOnlyCollection<int> componentIds, CancellationToken ct) =>
        await db.ComponentStocks.Where(s => componentIds.Contains(s.ComponentId)).ToListAsync(ct);

    public async Task<IReadOnlyList<StockItem>> FindStockItemsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct) =>
        await db.StockItems.Where(i => i.IsActive && itemIds.Contains(i.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<ProductionLogDto>> ListLogAsync(ProductionLogFilter filter, CancellationToken ct)
    {
        var log = db.ProductionLog.AsNoTracking();
        if (filter.ProductionId is { } productionId)
            log = log.Where(l => l.ProductionId == productionId);
        if (!string.IsNullOrEmpty(filter.Stage))
            log = log.Where(l => l.Stage == filter.Stage);

        return await (
            from l in log
            join m in db.ProductionModels on l.ProductionId equals m.Id into ms
            from m in ms.DefaultIfEmpty()
            orderby l.CreatedAt descending, l.Id descending
            select new ProductionLogDto(
                l.Id, l.ProductionId, m == null ? null : m.Name, l.SizeId, l.SizeName, l.Stage, l.Direction,
                l.Quantity, l.StockBefore, l.StockAfter, l.Comment, l.UserId, l.Username, l.CreatedAt)
        ).Take(Math.Clamp(filter.Limit, 1, 500)).ToListAsync(ct);
    }

    public void Add(ProductionLogEntry entry) => db.ProductionLog.Add(entry);

    public void Add(StockMovement movement) => db.StockMovements.Add(movement);

    public void Add(ComponentStock stock) => db.ComponentStocks.Add(stock);

    public void Add(ProductionModel model) => db.ProductionModels.Add(model);

    public void Add(ProductionComponent component) => db.ProductionComponents.Add(component);

    public void Remove(ProductionComponent component) => db.ProductionComponents.Remove(component);

    public void Remove(ComponentStock stock) => db.ComponentStocks.Remove(stock);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private IQueryable<ProductionModel> LoadModel() =>
        db.ProductionModels
            .Include(m => m.Sizes)
            .Include(m => m.ProductLinks)
            .Include(m => m.CategoryLinks)
            .Include(m => m.SewnBom)
            .AsSplitQuery(); // чотири колекції одним JOIN дали б добуток рядків, тож EF робить окремі запити
}
