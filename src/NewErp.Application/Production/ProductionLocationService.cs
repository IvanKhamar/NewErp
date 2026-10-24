using System.Globalization;

using NewErp.Application.Common;
using NewErp.Application.Locations;
using NewErp.Application.Stock;
using NewErp.Domain.Production;

namespace NewErp.Application.Production;

/// <summary>
/// Пошиті вироби на кількох складах. Пошитий залишок розміру лишається загальним (старий сайт, замовлення, вітрина);
/// що не лежить на жодному складі — це основний склад.
/// </summary>
public sealed class ProductionLocationService(
    IProductionRepository production, ISewnLocationRepository locations, ILocationStore warehouses, TimeProvider clock)
{
    private static readonly StringComparer Names = StringComparer.Create(CultureInfo.GetCultureInfo("uk-UA"), ignoreCase: true);

    /// <summary>Кожен розмір моделі з пошитими виробами за місцями.</summary>
    public async Task<IReadOnlyList<SizePlacesDto>> GetAsync(int productionId, CancellationToken ct)
    {
        var model = await production.GetDtoAsync(productionId, ct) ?? throw ModelNotFound(productionId);
        var bySize = (await locations.FindBalancesAsync(productionId, ct)).ToLookup(b => b.SizeId);
        var names = await NamesAsync(ct);
        return model.Sizes.Select(s => SizePlaces(s.Id, s.Name, s.SewnStock, bySize[s.Id], names)).ToList();
    }

    /// <summary>Переміщує пошиті вироби розміру між місцями; пошитий залишок і вітрина не змінюються.</summary>
    public async Task<IReadOnlyList<SizePlacesDto>> TransferAsync(int productionId, TransferSewnRequest request, int userId, string username, CancellationToken ct)
    {
        var names = await NamesAsync(ct);
        StockLocationService.RequireWarehouse(request.FromWarehouseId, names);
        StockLocationService.RequireWarehouse(request.ToWarehouseId, names);
        var placement = await PlacementAsync(productionId, ct);

        var transfer = placement.Move(
            request.SizeId, request.FromWarehouseId, request.ToWarehouseId, request.Quantity, request.Comment, userId, username, clock.GetLocalNow().DateTime);
        foreach (var balance in placement.Created)
            locations.Add(balance);
        locations.Add(transfer);
        await production.SaveChangesAsync(ct);

        return placement.Model.Sizes.OrderBy(s => s.SortOrder)
            .Select(s => SizePlaces(s.Id, s.Name, s.SewnStock, placement.Balances(s.Id), names))
            .ToList();
    }

    public async Task<IReadOnlyList<SewnTransferDto>> TransfersAsync(int productionId, CancellationToken ct)
    {
        var names = await NamesAsync(ct);
        var sizes = (await production.GetDtoAsync(productionId, ct))?.Sizes.ToDictionary(s => s.Id, s => s.Name) ?? [];
        return (await locations.ListTransfersAsync(productionId, ct))
            .Select(t => new SewnTransferDto(
                t.Id, t.SizeId, sizes.GetValueOrDefault(t.SizeId) ?? "", t.FromWarehouseId, StockLocationService.Name(t.FromWarehouseId, names),
                t.ToWarehouseId, StockLocationService.Name(t.ToWarehouseId, names), t.Quantity, t.Comment, t.Username, t.CreatedAt))
            .ToList();
    }

    /// <summary>Розміри активних моделей з виробами в кожному місці; стовпчик мають лише склади, де щось лежить.</summary>
    public async Task<SewnByWarehouseDto> ByWarehouseAsync(CancellationToken ct)
    {
        var names = await NamesAsync(ct);
        var bySize = (await locations.ListBalancesAsync(ct))
            .GroupBy(b => b.SizeId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(b => b.WarehouseId, b => b.Quantity));
        var columns = bySize.Values.SelectMany(b => b.Keys).Distinct()
            .OrderBy(id => StockLocationService.Name(id, names), Names)
            .ToList();

        var places = new List<SewnByWarehousePlace> { new(null, StockLocationService.MainName) };
        places.AddRange(columns.Select(id => new SewnByWarehousePlace(id, StockLocationService.Name(id, names))));
        var rows = (await locations.ListActiveSizesAsync(ct)).Select(s =>
        {
            var at = bySize.GetValueOrDefault(s.SizeId) ?? [];
            var quantities = new List<decimal>(columns.Count + 1) { s.Sewn - at.Values.Sum() };
            quantities.AddRange(columns.Select(id => at.GetValueOrDefault(id)));
            return new SewnByWarehouseRow(s.ProductionId, s.ModelName, s.SizeId, s.SizeName, s.Sewn, quantities);
        }).ToList();
        return new SewnByWarehouseDto(places, rows);
    }

    private async Task<SewnPlacement> PlacementAsync(int productionId, CancellationToken ct)
    {
        var model = await production.FindModelAsync(productionId, ct) ?? throw ModelNotFound(productionId);
        return new SewnPlacement(model, await locations.FindBalancesAsync(productionId, ct));
    }

    private static SizePlacesDto SizePlaces(int sizeId, string sizeName, decimal sewn, IEnumerable<SewnBalance> balances, IReadOnlyDictionary<int, string> names)
    {
        var list = balances.ToList();
        var places = new List<PlaceDto> { new(null, StockLocationService.MainName, sewn - list.Sum(b => b.Quantity)) };
        places.AddRange(list
            .Where(b => b.Quantity != 0)
            .OrderBy(b => StockLocationService.Name(b.WarehouseId, names), Names)
            .Select(b => new PlaceDto(b.WarehouseId, StockLocationService.Name(b.WarehouseId, names), b.Quantity)));
        return new SizePlacesDto(sizeId, sizeName, sewn, places);
    }

    private async Task<IReadOnlyDictionary<int, string>> NamesAsync(CancellationToken ct) =>
        (await warehouses.ListAsync(null, ct)).ToDictionary(w => w.Id, w => w.Name);

    private static NotFoundException ModelNotFound(int productionId) => new($"Модель {productionId} не знайдено.");
}
