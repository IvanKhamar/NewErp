using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Stock;

namespace NewErp.Application.Stock;

/// <summary>Сценарії складу розхідників.</summary>
public sealed class StockService(IStockRepository repository, TimeProvider clock)
{
    public async Task<int> CreateCategoryAsync(CreateStockCategoryRequest request, CancellationToken ct)
    {
        var category = new StockCategory(request.Name);
        if (await repository.CategoryNameTakenAsync(category.Name, ct))
            throw new DomainException($"Категорія \"{category.Name}\" вже існує.");

        repository.Add(category);
        await repository.SaveChangesAsync(ct);
        return category.Id;
    }

    public async Task<int> CreateSupplierAsync(CreateStockSupplierRequest request, CancellationToken ct)
    {
        var supplier = new StockSupplier(request.Name, request.Phone, request.Note);
        repository.Add(supplier);
        await repository.SaveChangesAsync(ct);
        return supplier.Id;
    }

    public async Task<int> CreateItemAsync(CreateStockItemRequest request, CancellationToken ct)
    {
        if (!await repository.CategoryExistsAsync(request.CategoryId, ct))
            throw new DomainException($"Категорії {request.CategoryId} не існує.");
        if (request.SupplierId is { } supplierId && !await repository.SupplierExistsAsync(supplierId, ct))
            throw new DomainException($"Постачальника {supplierId} не існує.");

        var item = new StockItem(request.CategoryId, request.SupplierId, request.Name, request.Unit, request.MinQuantity);
        repository.Add(item);
        await repository.SaveChangesAsync(ct);
        return item.Id;
    }

    public async Task<StockMovementDto> AdjustAsync(int itemId, AdjustStockRequest request, int userId, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(itemId, ct)
            ?? throw new NotFoundException($"Позицію складу {itemId} не знайдено.");

        var movement = item.Adjust(request.Delta, request.Reason, userId, clock.GetLocalNow().DateTime);
        repository.Add(movement);
        await repository.SaveChangesAsync(ct);

        return new StockMovementDto(movement.Id, item.Id, movement.Delta, movement.QuantityAfter, movement.Reason, movement.UserId, movement.CreatedAt);
    }

    public async Task SetMinQuantityAsync(int itemId, decimal minQuantity, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(itemId, ct)
            ?? throw new NotFoundException($"Позицію складу {itemId} не знайдено.");

        item.SetMinQuantity(minQuantity);
        await repository.SaveChangesAsync(ct);
    }
}