using Microsoft.EntityFrameworkCore;

using NewErp.Application.Common;
using NewErp.Application.Stock;
using NewErp.Domain.Stock;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Stock;

public sealed class StockRepository(ErpDbContext db) : IStockRepository
{
    public async Task<IReadOnlyList<LookupItem>> ListCategoriesAsync(CancellationToken ct) =>
        await db.StockCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new LookupItem(c.Id, c.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StockSupplierDto>> ListSuppliersAsync(CancellationToken ct) =>
        await db.StockSuppliers.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new StockSupplierDto(s.Id, s.Name, s.Phone, s.Email, s.Categories, s.Notes))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StockItemDto>> ListItemsAsync(StockItemFilter filter, CancellationToken ct)
    {
        var items = db.StockItems.AsNoTracking().Where(i => i.IsActive);
        if (filter.CategoryId is { } categoryId)
            items = items.Where(i => i.CategoryId == categoryId);
        if (filter.BelowMinimumOnly)
            items = items.Where(i => i.Quantity < i.MinQuantity);

        return await Project(items).ToListAsync(ct);
    }

    public Task<StockItemDto?> GetItemDtoAsync(int itemId, CancellationToken ct) =>
        Project(db.StockItems.AsNoTracking().Where(i => i.Id == itemId)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(int itemId, CancellationToken ct) =>
        await db.StockMovements.AsNoTracking()
            .Where(m => m.ItemId == itemId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Select(m => new StockMovementDto(
                m.Id, m.ItemId, m.Action == StockMovement.Use ? -m.Quantity : m.Quantity,
                m.StockBefore, m.StockAfter, m.Comment, m.UserId, m.Username, m.CreatedAt))
            .ToListAsync(ct);

    public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken ct) =>
        db.StockCategories.AnyAsync(c => c.Id == categoryId, ct);

    public Task<bool> CategoryNameTakenAsync(string name, CancellationToken ct) =>
        db.StockCategories.AnyAsync(c => c.Name == name, ct);

    public Task<bool> SupplierExistsAsync(int supplierId, CancellationToken ct) =>
        db.StockSuppliers.AnyAsync(s => s.Id == supplierId, ct);

    public Task<StockItem?> FindItemAsync(int itemId, CancellationToken ct) =>
        db.StockItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);

    public void Add(StockCategory category) => db.StockCategories.Add(category);

    public void Add(StockSupplier supplier) => db.StockSuppliers.Add(supplier);

    public void Add(StockItem item) => db.StockItems.Add(item);

    public void Add(StockMovement movement) => db.StockMovements.Add(movement);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    // Категорія й постачальник необов'язкові (у старому модулі NULL), тому обидва з'єднання LEFT JOIN.
    private IQueryable<StockItemDto> Project(IQueryable<StockItem> items) =>
        from i in items
        join c in db.StockCategories on i.CategoryId equals (int?)c.Id into cs
        from c in cs.DefaultIfEmpty()
        join s in db.StockSuppliers on i.SupplierId equals (int?)s.Id into ss
        from s in ss.DefaultIfEmpty()
        orderby i.Name
        select new StockItemDto(
            i.Id, i.Name, i.Unit, i.CategoryId, c == null ? null : c.Name,
            i.SupplierId, s == null ? null : s.Name,
            i.Quantity, i.MinQuantity, i.Quantity < i.MinQuantity, i.UsedFor, i.Price);
}