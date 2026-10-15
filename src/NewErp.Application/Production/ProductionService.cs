using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Production;

namespace NewErp.Application.Production;

/// <summary>Сценарії виробництва (перенесено з модуля StockTrack старого сайту): моделі, розміри, деталі крою, специфікації.</summary>
public sealed class ProductionService(IProductionRepository repository)
{
    public async Task<int> CreateModelAsync(SaveProductionModelRequest request, CancellationToken ct)
    {
        var model = new ProductionModel(request.Name, request.Type, request.MinTotal, request.SortOrder);
        model.SetSizes(ToSizes(request));
        repository.Add(model);
        await repository.SaveChangesAsync(ct);
        return model.Id;
    }

    public async Task UpdateModelAsync(int productionId, SaveProductionModelRequest request, CancellationToken ct)
    {
        var model = await RequireModelAsync(productionId, ct);
        model.Update(request.Name, request.Type, request.MinTotal, request.SortOrder);

        var removed = model.SetSizes(ToSizes(request));
        if (removed.Count > 0 && await repository.SizesInUseAsync(removed, ct))
            throw new DomainException("Прибраний розмір використовується в специфікації крою або має розкроєні деталі.");

        await repository.SaveChangesAsync(ct);
    }

    /// <summary>Як у старому модулі: модель не видаляється, а позначається неактивною.</summary>
    public async Task DeactivateModelAsync(int productionId, CancellationToken ct)
    {
        (await RequireModelAsync(productionId, ct)).Deactivate();
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetLinksAsync(int productionId, SetProductionLinksRequest request, CancellationToken ct)
    {
        var model = await RequireModelAsync(productionId, ct);
        model.SetLinks(
            request.ProductIds ?? [],
            (request.Categories ?? []).Select(c => (c.CategoryId, c.NameFilter, c.MatchExact)).ToList());
        model.SetSizeOptions((request.SizeOptions ?? []).Select(o => (o.SizeId, o.OptionValueId)).ToList());
        await repository.SaveChangesAsync(ct);
    }

    public async Task<int> AddComponentAsync(int productionId, SaveComponentRequest request, CancellationToken ct)
    {
        await RequireModelAsync(productionId, ct);
        var component = new ProductionComponent(productionId, request.Name, request.SortOrder);
        repository.Add(component);
        await repository.SaveChangesAsync(ct);
        return component.Id;
    }

    public async Task UpdateComponentAsync(int componentId, SaveComponentRequest request, CancellationToken ct)
    {
        (await RequireComponentAsync(componentId, ct)).Rename(request.Name, request.SortOrder);
        await repository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Видаляє деталь разом з її специфікацією крою, запасом і рядками специфікації пошиву.
    /// На відміну від старого модуля, деталь із ненульовим запасом видалити не можна, щоб не загубити розкроєне.
    /// </summary>
    public async Task DeleteComponentAsync(int componentId, CancellationToken ct)
    {
        var component = await RequireComponentAsync(componentId, ct);
        var stock = await repository.FindComponentStockAsync(componentId, ct);
        if (stock.Any(s => s.Stock != 0))
            throw new DomainException($"Деталь \"{component.Name}\" має розкроєний запас, її не можна видалити.");

        var model = await RequireModelAsync(component.ProductionId, ct);
        model.RemoveComponentFromSewnBom(componentId);
        foreach (var row in stock)
            repository.Remove(row);
        repository.Remove(component);
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetComponentBomAsync(int componentId, SetComponentBomRequest request, CancellationToken ct)
    {
        var component = await RequireComponentAsync(componentId, ct);
        var model = await RequireModelAsync(component.ProductionId, ct);
        var rows = (request.Rows ?? []).Select(r => (r.SizeId, r.ItemId, r.QtyPerUnit)).ToList();
        await RequireItemsAsync(rows.Select(r => r.ItemId), ct);

        component.SetBom(rows, model.Sizes.Select(s => s.Id).ToList());
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetSewnBomAsync(int productionId, SetSewnBomRequest request, CancellationToken ct)
    {
        var model = await RequireModelAsync(productionId, ct);
        var rows = (request.Rows ?? []).Select(r => (r.SizeId, r.ComponentId, r.ItemId, r.QtyPerUnit)).ToList();
        await RequireItemsAsync(rows.Where(r => r.ItemId is not null).Select(r => r.ItemId!.Value), ct);

        model.SetSewnBom(rows, await repository.ComponentIdsAsync(productionId, ct));
        await repository.SaveChangesAsync(ct);
    }

    private static List<(string, decimal)> ToSizes(SaveProductionModelRequest request) =>
        (request.Sizes ?? []).Select(s => (s.Name, s.Percent)).ToList();

    private async Task RequireItemsAsync(IEnumerable<int> itemIds, CancellationToken ct)
    {
        var missing = await repository.MissingStockItemIdsAsync(itemIds.Distinct().ToList(), ct);
        if (missing.Count > 0)
            throw new DomainException($"Розхідників {string.Join(", ", missing)} немає на складі.");
    }

    private async Task<ProductionModel> RequireModelAsync(int productionId, CancellationToken ct) =>
        await repository.FindModelAsync(productionId, ct) is { IsActive: true } model
            ? model
            : throw new NotFoundException($"Модель {productionId} не знайдено.");

    private async Task<ProductionComponent> RequireComponentAsync(int componentId, CancellationToken ct) =>
        await repository.FindComponentAsync(componentId, ct)
            ?? throw new NotFoundException($"Деталь {componentId} не знайдено.");
}
