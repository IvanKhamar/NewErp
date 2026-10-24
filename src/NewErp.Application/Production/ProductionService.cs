using NewErp.Application.Common;
using NewErp.Application.Locations;
using NewErp.Domain.Common;
using NewErp.Domain.Production;
using NewErp.Domain.Stock;

namespace NewErp.Application.Production;

/// <summary>
/// Сценарії виробництва (на основі модуля StockTrack старого сайту): моделі, розміри, деталі крою, специфікації,
/// операції крою й пошиву зі списанням розхідників.
/// </summary>
public sealed class ProductionService(
    IProductionRepository repository, IStorefrontStock storefront, ISewnLocationRepository locations, ILocationStore warehouses, TimeProvider clock)
{
    /// <summary>
    /// Виконує операцію для кількох розмірів одразу. Крій і пошив списують розхідники та деталі за специфікаціями:
    /// якщо чогось бракує, списується те, що є, а нестача повертається в Warnings. Усе зберігається одним записом.
    /// </summary>
    public async Task<ProductionOperationResult> RecordAsync(
        int productionId, ProductionOperation operation, ProductionOperationRequest request, int userId, string username, CancellationToken ct)
    {
        var model = await RequireModelAsync(productionId, ct);
        var rows = request.Quantities ?? [];
        if (rows.Count == 0)
            throw new DomainException("Вкажіть кількість хоча б для одного розміру.");
        if (rows.GroupBy(r => r.SizeId).FirstOrDefault(g => g.Count() > 1) is { } dup)
            throw new DomainException($"Розмір {dup.Key} указано двічі.");

        if (request.WarehouseId is { } warehouseId)
        {
            if (operation is not (ProductionOperation.Sewn or ProductionOperation.Ship))
                throw new DomainException("Склад вказують лише для пошитого й відвантаженого: крій живе на основному складі.");
            if (await warehouses.GetAsync(warehouseId, ct) is null)
                throw new NotFoundException($"Склад {warehouseId} не знайдено.");
        }
        // Пошиті вироби мають місця: відвантаження з основного складу не бере того, що лежить на інших.
        var placement = operation is ProductionOperation.Sewn or ProductionOperation.Ship
            ? new SewnPlacement(model, await locations.FindBalancesAsync(productionId, ct))
            : null;

        var now = clock.GetLocalNow().DateTime;
        var consumption = operation is ProductionOperation.Cut or ProductionOperation.Sewn
            ? await LoadConsumptionAsync(model, ct)
            : null;
        var changes = new List<SizeChangeDto>();

        foreach (var (sizeId, quantity) in rows)
        {
            var entry = operation switch
            {
                ProductionOperation.Cut => model.RecordCut(sizeId, quantity, request.Comment, userId, username, now),
                ProductionOperation.SendToSewing => model.SendToSewing(sizeId, quantity, request.Comment, userId, username, now),
                ProductionOperation.Sewn => placement!.SewnAt(sizeId, request.WarehouseId, quantity, request.Comment, userId, username, now),
                ProductionOperation.Ship => placement!.ShipFrom(sizeId, request.WarehouseId, quantity, request.Comment, userId, username, now),
                _ => throw new DomainException($"Невідома операція {operation}."),
            };
            repository.Add(entry);
            changes.Add(new SizeChangeDto(sizeId, entry.SizeName, entry.StockBefore, entry.StockAfter));

            if (operation == ProductionOperation.Cut)
                consumption!.ForCut(entry.SizeName, sizeId, quantity, userId, username, now);
            else if (operation == ProductionOperation.Sewn)
                consumption!.ForSewing(entry.SizeName, sizeId, quantity, userId, username, now);
        }

        foreach (var balance in placement?.Created ?? [])
            locations.Add(balance);
        await repository.SaveChangesAsync(ct);
        // Як у старому модулі: після зміни пошитого залишку він одразу йде на вітрину.
        if (operation is ProductionOperation.Sewn or ProductionOperation.Ship)
            await storefront.PushAsync([productionId], ct);
        return new ProductionOperationResult(changes, consumption?.Warnings ?? []);
    }

    private async Task<Consumption> LoadConsumptionAsync(ProductionModel model, CancellationToken ct)
    {
        var components = await repository.FindComponentsAsync(model.Id, ct);
        var itemIds = components.SelectMany(c => c.Bom.Select(b => b.ItemId))
            .Concat(model.SewnBom.Where(b => b.ItemId is not null).Select(b => b.ItemId!.Value))
            .Distinct().ToList();
        var items = await repository.FindStockItemsAsync(itemIds, ct);
        var stocks = await repository.FindComponentStocksAsync(components.Select(c => c.Id).ToList(), ct);
        return new Consumption(repository, model, components, items, stocks);
    }

    /// <summary>Списання розхідників і деталей для однієї операції (стан, спільний для всіх розмірів запиту).</summary>
    private sealed class Consumption(
        IProductionRepository repository,
        ProductionModel model,
        IReadOnlyList<ProductionComponent> components,
        IReadOnlyList<StockItem> items,
        IReadOnlyList<ComponentStock> stocks)
    {
        private readonly Dictionary<int, StockItem> _items = items.ToDictionary(i => i.Id);
        private readonly Dictionary<(int, int), ComponentStock> _stocks = stocks.ToDictionary(s => (s.ComponentId, s.SizeId));

        public List<string> Warnings { get; } = [];

        /// <summary>+Покроєно: кожна деталь отримує +quantity у запас, а її розхідники списуються за специфікацією крою.</summary>
        public void ForCut(string sizeName, int sizeId, decimal quantity, int userId, string username, DateTime now)
        {
            foreach (var component in components)
            {
                foreach (var line in BomResolver.Resolve(component.Bom, sizeId, b => b.SizeId, b => b.ItemId))
                    TakeItem(line.ItemId, line.QtyPerUnit * quantity, Invariant($"Крій: {model.Name}, {component.Name}, {sizeName} x{quantity:0.###}"), userId, username, now);
                StockOf(component.Id, sizeId).Add(quantity);
            }
        }

        /// <summary>+Пошито: деталі крою береться із запасу, прямі розхідники (нитки, бірки) списуються зі складу.</summary>
        public void ForSewing(string sizeName, int sizeId, decimal quantity, int userId, string username, DateTime now)
        {
            var componentLines = BomResolver.Resolve(model.SewnBom, sizeId, b => b.SizeId, b => b.ComponentId);
            foreach (var line in componentLines)
            {
                var need = line.QtyPerUnit * quantity;
                var taken = StockOf(line.ComponentId!.Value, sizeId).TakeUpTo(need);
                if (taken < need)
                {
                    var name = components.FirstOrDefault(c => c.Id == line.ComponentId)?.Name ?? $"деталь {line.ComponentId}";
                    Warnings.Add(Invariant($"Бракує розкроєних деталей «{name}» розміру {sizeName}: {need - taken:0.###} шт. Взято {taken:0.###} з {need:0.###}."));
                }
            }

            foreach (var line in BomResolver.Resolve(model.SewnBom, sizeId, b => b.SizeId, b => b.ItemId))
                TakeItem(line.ItemId!.Value, line.QtyPerUnit * quantity, Invariant($"Пошив: {model.Name}, {sizeName} x{quantity:0.###}"), userId, username, now);
        }

        private void TakeItem(int itemId, decimal need, string comment, int userId, string username, DateTime now)
        {
            if (!_items.TryGetValue(itemId, out var item))
            {
                Warnings.Add($"Розхідника {itemId} зі специфікації немає на складі, його не списано.");
                return;
            }

            // Довгі назви моделі й деталі можуть не вміститися в коментар журналу (255 символів).
            var movement = item.TakeUpTo(need, comment.Length > 255 ? comment[..255] : comment, userId, username, now);
            if (movement is not null)
                repository.Add(movement);
            var taken = movement?.Quantity ?? 0;
            if (taken < need)
                Warnings.Add(Invariant($"Бракує {need - taken:0.###} {item.Unit}: {item.Name}. Списано {taken:0.###} з {need:0.###}."));
        }

        private ComponentStock StockOf(int componentId, int sizeId)
        {
            if (!_stocks.TryGetValue((componentId, sizeId), out var stock))
            {
                stock = new ComponentStock(componentId, sizeId);
                _stocks[(componentId, sizeId)] = stock;
                repository.Add(stock);
            }
            return stock;
        }

        private static string Invariant(FormattableString text) => FormattableString.Invariant(text);
    }

    /// <summary>Передати на вітрину пошиті залишки всіх активних моделей (наприклад, після змін на старому сайті).</summary>
    public Task<StorefrontSyncResult> PushStorefrontAsync(CancellationToken ct) => storefront.PushAsync(null, ct);

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
        await storefront.PushAsync([productionId], ct);
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
