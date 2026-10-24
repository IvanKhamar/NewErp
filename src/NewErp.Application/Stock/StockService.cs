using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Stock;

namespace NewErp.Application.Stock;

/// <summary>Сценарії складу розхідників.</summary>
public sealed class StockService(IStockRepository repository, IStockLocationRepository locations, TimeProvider clock)
{
    public async Task<int> CreateCategoryAsync(CreateStockCategoryRequest request, CancellationToken ct)
    {
        var category = new StockCategory(request.Name, request.SortOrder);
        if (await repository.CategoryNameTakenAsync(category.Name, ct))
            throw new DomainException($"Категорія \"{category.Name}\" вже існує.");

        repository.Add(category);
        await repository.SaveChangesAsync(ct);
        return category.Id;
    }

    public async Task<int> CreateSupplierAsync(CreateStockSupplierRequest request, CancellationToken ct)
    {
        var supplier = new StockSupplier(request.Name, request.Phone, request.Email, request.Categories, request.Notes);
        repository.Add(supplier);
        await repository.SaveChangesAsync(ct);
        return supplier.Id;
    }

    public async Task<int> CreateItemAsync(SaveStockItemRequest request, CancellationToken ct)
    {
        await RequireReferencesAsync(request, ct);
        var item = new StockItem(
            request.CategoryId, request.SupplierId, request.Name, request.Unit, request.MinQuantity, request.UsedFor, request.Price);
        repository.Add(item);
        await repository.SaveChangesAsync(ct);
        return item.Id;
    }

    /// <summary>Змінює опис позиції (назва, категорія, постачальник, ціна, мінімум). Залишок не чіпає.</summary>
    public async Task UpdateItemAsync(int itemId, SaveStockItemRequest request, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(itemId, ct)
            ?? throw new NotFoundException($"Позицію складу {itemId} не знайдено.");
        await RequireReferencesAsync(request, ct);

        item.Update(request.CategoryId, request.SupplierId, request.Name, request.Unit, request.MinQuantity, request.UsedFor, request.Price);
        await repository.SaveChangesAsync(ct);
    }

    private async Task RequireReferencesAsync(SaveStockItemRequest request, CancellationToken ct)
    {
        if (request.CategoryId is { } categoryId && !await repository.CategoryExistsAsync(categoryId, ct))
            throw new DomainException($"Категорії {categoryId} не існує.");
        if (request.SupplierId is { } supplierId && !await repository.SupplierExistsAsync(supplierId, ct))
            throw new DomainException($"Постачальника {supplierId} не існує.");
    }

    public async Task<StockMovementDto> AdjustAsync(int itemId, AdjustStockRequest request, int userId, string username, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(itemId, ct)
            ?? throw new NotFoundException($"Позицію складу {itemId} не знайдено.");

        // Без складу — це основний склад: те, що лежить на інших складах, звідси не витрачається.
        var m = new StockPlacement(item, await locations.FindBalancesAsync(itemId, ct))
            .AdjustAt(null, request.Delta, request.Reason, userId, username, clock.GetLocalNow().DateTime);
        repository.Add(m);
        await repository.SaveChangesAsync(ct);

        return new StockMovementDto(m.Id, item.Id, m.Delta, m.StockBefore, m.StockAfter, m.Comment, m.UserId, m.Username, m.CreatedAt);
    }

    public async Task SetMinQuantityAsync(int itemId, decimal minQuantity, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(itemId, ct)
            ?? throw new NotFoundException($"Позицію складу {itemId} не знайдено.");

        item.SetMinQuantity(minQuantity);
        await repository.SaveChangesAsync(ct);
    }
}