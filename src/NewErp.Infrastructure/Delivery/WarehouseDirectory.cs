using Microsoft.EntityFrameworkCore;

using NewErp.Application.Delivery;
using NewErp.Domain.Delivery;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Delivery;

/// <summary>
/// Довідник відділень у dd_novaposhta_warehouse. Окремої таблиці міст немає: як і на старому сайті,
/// місто — це city_ref + city_description відділень.
/// </summary>
public sealed class WarehouseDirectory(ErpDbContext db) : IWarehouseDirectory
{
    /// <summary>По скільки рядків вставляти при повному оновленні (у довіднику десятки тисяч відділень).</summary>
    private const int InsertBatchSize = 1000;

    public async Task<IReadOnlyList<CityDto>> SearchCitiesAsync(string search, int limit, CancellationToken ct) =>
        await db.NovaPoshtaWarehouses.AsNoTracking()
            .Where(w => w.Status && w.CityDescription.StartsWith(search))
            .GroupBy(w => new { w.CityRef, w.CityDescription })
            .OrderBy(g => g.Key.CityDescription)
            .Take(limit)
            .Select(g => new CityDto(g.Key.CityRef, g.Key.CityDescription))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WarehouseDto>> SearchWarehousesAsync(string cityRef, string search, int limit, CancellationToken ct) =>
        await db.NovaPoshtaWarehouses.AsNoTracking()
            .Where(w => w.CityRef == cityRef && w.Status && (search == "" || w.Description.Contains(search)))
            .OrderBy(w => w.SortOrder).ThenBy(w => w.Description)
            .Take(limit)
            .Select(w => new WarehouseDto(w.Ref, w.Description, w.Category, w.MaxWeight))
            .ToListAsync(ct);

    public Task<CityDto?> FindCityAsync(string name, CancellationToken ct) =>
        db.NovaPoshtaWarehouses.AsNoTracking()
            .Where(w => w.Status && w.CityDescription == name)
            .Select(w => new CityDto(w.CityRef, w.CityDescription))
            .FirstOrDefaultAsync(ct);

    public Task<WarehouseDto?> FindWarehouseAsync(string cityRef, string name, CancellationToken ct) =>
        db.NovaPoshtaWarehouses.AsNoTracking()
            .Where(w => w.CityRef == cityRef && w.Status && w.Description == name)
            .Select(w => new WarehouseDto(w.Ref, w.Description, w.Category, w.MaxWeight))
            .FirstOrDefaultAsync(ct);

    public Task<bool> WarehouseExistsAsync(string cityRef, string warehouseRef, CancellationToken ct) =>
        db.NovaPoshtaWarehouses.AnyAsync(w => w.Ref == warehouseRef && w.CityRef == cityRef && w.Status, ct);

    public async Task ReplaceAllAsync(IReadOnlyList<NovaPoshtaWarehouse> warehouses, CancellationToken ct)
    {
        // Транзакція: вітрина старого сайту не має побачити порожній довідник посеред оновлення.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.NovaPoshtaWarehouses.ExecuteDeleteAsync(ct);
        foreach (var batch in warehouses.DistinctBy(w => w.Ref).Chunk(InsertBatchSize))
        {
            db.NovaPoshtaWarehouses.AddRange(batch);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear(); // не тримати в пам'яті вже вставлені десятки тисяч об'єктів
        }
        await transaction.CommitAsync(ct);
    }
}
