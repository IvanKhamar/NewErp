using Microsoft.EntityFrameworkCore;

using NewErp.Application.Production;
using NewErp.Domain.Production;
using NewErp.Domain.Stock;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Production;

public sealed class ProductionRepository(ErpDbContext db) : IProductionRepository
{
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
