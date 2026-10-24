using System.Globalization;

using NewErp.Application.Common;
using NewErp.Application.Locations;
using NewErp.Domain.Stock;

namespace NewErp.Application.Stock;

/// <summary>
/// Розхідники на кількох складах (склади — довідник «Склади й пункти видачі»). Загальний залишок лишається в dd_stock_items,
/// тому старий сайт і решта операцій працюють як раніше; що не лежить на жодному складі — це основний склад.
/// </summary>
public sealed class StockLocationService(
    IStockRepository stock, IStockLocationRepository locations, ILocationStore warehouses, TimeProvider clock)
{
    public const string MainName = "Основний склад";

    private static readonly StringComparer Names = StringComparer.Create(CultureInfo.GetCultureInfo("uk-UA"), ignoreCase: true);

    /// <summary>Склади, куди можна покласти чи звідки взяти, для списків вибору.</summary>
    public async Task<IReadOnlyList<LookupItem>> WarehousesAsync(CancellationToken ct) =>
        (await warehouses.ListAsync(null, ct)).Select(w => new LookupItem(w.Id, w.Name)).OrderBy(w => w.Name, Names).ToList();

    public async Task<ItemPlacementDto> GetAsync(int itemId, CancellationToken ct)
    {
        var item = await stock.GetItemDtoAsync(itemId, ct) ?? throw ItemNotFound(itemId);
        var balances = await locations.FindBalancesAsync(itemId, ct);
        return Dto(item.Id, item.Name, item.Unit, item.Quantity, item.Quantity - balances.Sum(b => b.Quantity), balances, await NamesAsync(ct));
    }

    /// <summary>Переміщує між двома місцями; загальний залишок той самий, переміщення в журналі переміщень.</summary>
    public async Task<ItemPlacementDto> TransferAsync(int itemId, TransferStockRequest request, int userId, string username, CancellationToken ct)
    {
        var names = await NamesAsync(ct);
        RequireWarehouse(request.FromWarehouseId, names);
        RequireWarehouse(request.ToWarehouseId, names);
        var placement = await PlacementAsync(itemId, ct);

        var transfer = placement.Move(request.FromWarehouseId, request.ToWarehouseId, request.Quantity, request.Comment, userId, username, Now());
        AddCreated(placement);
        locations.Add(transfer);
        await stock.SaveChangesAsync(ct);
        return Dto(placement, names);
    }

    /// <summary>Прихід (+) або витрата (−) на складі; у журналі руху — з назвою складу.</summary>
    public async Task<StockMovementDto> AdjustAsync(int itemId, int warehouseId, AdjustStockRequest request, int userId, string username, CancellationToken ct)
    {
        var names = await NamesAsync(ct);
        RequireWarehouse(warehouseId, names);
        var placement = await PlacementAsync(itemId, ct);

        // Порожню причину передаємо як є, щоб домен відмовив зі звичним повідомленням.
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? request.Reason : Truncate($"{request.Reason.Trim()} · {names[warehouseId]}", 255);
        var m = placement.AdjustAt(warehouseId, request.Delta, reason, userId, username, Now());
        AddCreated(placement);
        stock.Add(m);
        await stock.SaveChangesAsync(ct);
        return new StockMovementDto(m.Id, itemId, m.Delta, m.StockBefore, m.StockAfter, m.Comment, m.UserId, m.Username, m.CreatedAt);
    }

    public async Task<IReadOnlyList<StockTransferDto>> TransfersAsync(int itemId, CancellationToken ct)
    {
        var names = await NamesAsync(ct);
        return (await locations.ListTransfersAsync(itemId, ct))
            .Select(t => new StockTransferDto(
                t.Id, t.FromWarehouseId, Name(t.FromWarehouseId, names), t.ToWarehouseId, Name(t.ToWarehouseId, names),
                t.Quantity, t.Comment, t.Username, t.CreatedAt))
            .ToList();
    }

    /// <summary>Кожна активна позиція із залишком у кожному місці; стовпчик мають лише склади, де щось лежить.</summary>
    public async Task<StockByWarehouseDto> ByWarehouseAsync(CancellationToken ct)
    {
        var names = await NamesAsync(ct);
        var items = await stock.ListItemsAsync(new StockItemFilter(), ct);
        var balances = (await locations.ListBalancesAsync(ct))
            .GroupBy(b => b.ItemId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(b => b.WarehouseId, b => b.Quantity));
        var columns = balances.Values.SelectMany(b => b.Keys).Distinct()
            .OrderBy(id => Name(id, names), Names)
            .ToList();

        var values = new decimal[columns.Count + 1]; // 0 — основний склад
        var rows = new List<StockByWarehouseRow>(items.Count);
        foreach (var item in items)
        {
            var at = balances.GetValueOrDefault(item.Id) ?? [];
            var quantities = new List<decimal>(columns.Count + 1) { item.Quantity - at.Values.Sum() };
            quantities.AddRange(columns.Select(id => at.GetValueOrDefault(id)));
            for (var i = 0; i < quantities.Count; i++)
                values[i] += Math.Max(quantities[i], 0) * item.Price;
            rows.Add(new StockByWarehouseRow(item.Id, item.Name, item.Unit, item.CategoryName, item.Quantity, quantities));
        }

        var places = new List<StockByWarehousePlace> { new(null, MainName, Money(values[0])) };
        places.AddRange(columns.Select((id, i) => new StockByWarehousePlace(id, Name(id, names), Money(values[i + 1]))));
        return new StockByWarehouseDto(places, rows);
    }

    private async Task<StockPlacement> PlacementAsync(int itemId, CancellationToken ct)
    {
        var item = await stock.FindItemAsync(itemId, ct);
        if (item is not { IsActive: true })
            throw ItemNotFound(itemId);
        return new StockPlacement(item, await locations.FindBalancesAsync(itemId, ct));
    }

    private void AddCreated(StockPlacement placement)
    {
        foreach (var balance in placement.Created)
            locations.Add(balance);
    }

    private static ItemPlacementDto Dto(StockPlacement placement, IReadOnlyDictionary<int, string> names) =>
        Dto(placement.Item.Id, placement.Item.Name, placement.Item.Unit, placement.Item.Quantity, placement.Main, placement.Balances, names);

    private static ItemPlacementDto Dto(
        int itemId, string name, string unit, decimal total, decimal main, IEnumerable<StockBalance> balances, IReadOnlyDictionary<int, string> names)
    {
        var places = new List<PlaceDto> { new(null, MainName, main) };
        places.AddRange(balances
            .Where(b => b.Quantity != 0)
            .OrderBy(b => Name(b.WarehouseId, names), Names)
            .Select(b => new PlaceDto(b.WarehouseId, Name(b.WarehouseId, names), b.Quantity)));
        return new ItemPlacementDto(itemId, name, unit, total, places);
    }

    private async Task<IReadOnlyDictionary<int, string>> NamesAsync(CancellationToken ct) =>
        (await warehouses.ListAsync(null, ct)).ToDictionary(w => w.Id, w => w.Name);

    internal static void RequireWarehouse(int? warehouseId, IReadOnlyDictionary<int, string> names)
    {
        if (warehouseId is { } id && !names.ContainsKey(id))
            throw new NotFoundException($"Склад {id} не знайдено.");
    }

    internal static string Name(int? warehouseId, IReadOnlyDictionary<int, string> names) =>
        warehouseId is { } id ? names.GetValueOrDefault(id) ?? $"Склад {id}" : MainName;

    private static NotFoundException ItemNotFound(int itemId) => new($"Позицію складу {itemId} не знайдено.");

    private static string Truncate(string text, int length) => text.Length > length ? text[..length] : text;

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private DateTime Now() => clock.GetLocalNow().DateTime;
}
