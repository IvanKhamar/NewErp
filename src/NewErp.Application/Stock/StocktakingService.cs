using NewErp.Application.Common;
using NewErp.Application.Locations;
using NewErp.Domain.Common;
using NewErp.Domain.Stock;

namespace NewErp.Application.Stock;

/// <summary>Інвентаризація розхідників: аркуш для підрахунку, перегляд завантаженого й застосування з записами в журнал.</summary>
public sealed class StocktakingService(
    IStockRepository repository, IStockLocationRepository locations, ILocationStore warehouses, IStocktakingSheet sheet, TimeProvider clock)
{
    public const int MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>Що за обліком лежить у місці для кожної активної позиції: на складі — його баланс, на основному — решта.</summary>
    public async Task<IReadOnlyDictionary<int, decimal>> BookedAsync(int? warehouseId, CancellationToken ct)
    {
        await RequireWarehouseAsync(warehouseId, ct);
        var located = (await locations.ListBalancesAsync(ct))
            .Where(b => warehouseId is null || b.WarehouseId == warehouseId)
            .GroupBy(b => b.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.Quantity));
        return (await repository.ListItemsAsync(new StockItemFilter(), ct)).ToDictionary(
            i => i.Id,
            i => warehouseId is null ? i.Quantity - located.GetValueOrDefault(i.Id) : located.GetValueOrDefault(i.Id));
    }

    /// <summary>Активні позиції (однієї категорії, якщо вказано) з кількістю за обліком і порожнім стовпчиком «Фактично».</summary>
    public async Task<byte[]> SheetAsync(int? categoryId, int? warehouseId, CancellationToken ct)
    {
        var booked = await BookedAsync(warehouseId, ct);
        var rows = (await repository.ListItemsAsync(new StockItemFilter(categoryId), ct))
            .Select(i => new StocktakingSheetRow(i.Id, i.Name, i.CategoryName ?? "", i.Unit, booked[i.Id]))
            .ToList();
        return sheet.Write(Stocktaking.Columns, rows);
    }

    /// <summary>Читає аркуш, порахований у місці; нічого не записує.</summary>
    public async Task<StocktakingPreviewDto> PreviewAsync(Stream file, int? warehouseId, CancellationToken ct)
    {
        var parsed = Stocktaking.Parse(sheet.Read(file));
        var items = (await repository.ListItemsAsync(new StockItemFilter(), ct)).ToDictionary(i => i.Id);
        var booked = await BookedAsync(warehouseId, ct);
        var errors = parsed.Errors.ToList();
        var lines = new List<StocktakingLineDto>();
        foreach (var count in parsed.Counts)
        {
            if (!items.TryGetValue(count.ItemId, out var item))
            {
                errors.Add($"Позиції {count.ItemId} немає на складі.");
                continue;
            }
            var before = booked[item.Id];
            lines.Add(new StocktakingLineDto(item.Id, item.Name, item.Unit, before, count.Actual, count.Actual - before));
        }
        return new StocktakingPreviewDto(lines.OrderBy(l => l.Name).ToList(), errors);
    }

    /// <summary>
    /// Ставить кожній порахованій позиції в місці пораховану кількість одним записом; загальний залишок змінюється на ту саму
    /// різницю, у журналі — «Інвентаризація». Якщо хоч одна кількість неправильна, нічого не записується.
    /// </summary>
    public async Task<StocktakingResult> ApplyAsync(ApplyStocktakingRequest request, int userId, string username, CancellationToken ct)
    {
        var counts = request.Counts ?? [];
        if (counts.Count == 0)
            throw new DomainException("Немає жодної порахованої позиції.");
        if (counts.GroupBy(c => c.ItemId).FirstOrDefault(g => g.Count() > 1) is { } dup)
            throw new DomainException($"Позицію {dup.Key} указано двічі.");
        await RequireWarehouseAsync(request.WarehouseId, ct);
        var comment = Stocktaking.Comment(request.Comment);

        var now = clock.GetLocalNow().DateTime;
        var changed = 0;
        var value = 0m;
        foreach (var count in counts.OrderBy(c => c.ItemId))
        {
            var item = await repository.FindItemAsync(count.ItemId, ct);
            if (item is not { IsActive: true })
                throw new DomainException($"Позиції {count.ItemId} немає на складі.");
            var placement = new StockPlacement(item, await locations.FindBalancesAsync(item.Id, ct));
            StockMovement? movement;
            try
            {
                movement = placement.RecountAt(request.WarehouseId, count.Actual, comment, userId, username, now);
            }
            catch (DomainException e)
            {
                throw new DomainException($"{item.Name}: {e.Message}");
            }
            foreach (var balance in placement.Created)
                locations.Add(balance);
            if (movement is not null)
            {
                repository.Add(movement);
                changed++;
                value += movement.Delta * item.Price;
            }
        }
        await repository.SaveChangesAsync(ct);
        return new StocktakingResult(counts.Count, changed, Math.Round(value, 2, MidpointRounding.AwayFromZero));
    }

    private async Task RequireWarehouseAsync(int? warehouseId, CancellationToken ct)
    {
        if (warehouseId is { } id && await warehouses.GetAsync(id, ct) is null)
            throw new NotFoundException($"Склад {id} не знайдено.");
    }
}
