using Microsoft.EntityFrameworkCore;

using NewErp.Application.Locations;
using NewErp.Domain.Locations;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Locations;

public sealed class LocationStore(ErpDbContext db) : ILocationStore
{
    public async Task<IReadOnlyList<LocationDto>> ListAsync(string? search, CancellationToken ct)
    {
        var query = db.DdWarehouses.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(w => w.Name.Contains(search) || w.City.Contains(search) || w.Address.Contains(search) || w.Type.Contains(search));
        return await query.OrderBy(w => w.WarehouseId).Select(w => ToDto(w)).ToListAsync(ct);
    }

    public Task<LocationDto?> GetAsync(int id, CancellationToken ct) =>
        db.DdWarehouses.AsNoTracking().Where(w => w.WarehouseId == id).Select(w => ToDto(w)).FirstOrDefaultAsync(ct);

    public async Task<int> AddAsync(ShopLocationData data, CancellationToken ct)
    {
        var entity = new DdWarehouse { Image = "" };
        Apply(entity, data);
        db.DdWarehouses.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.WarehouseId;
    }

    // Картинку (image) NewErp не змінює: її завантажують в адмінці сайту.
    public async Task<bool> UpdateAsync(int id, ShopLocationData data, CancellationToken ct) =>
        await db.DdWarehouses.Where(w => w.WarehouseId == id).ExecuteUpdateAsync(s => s
            .SetProperty(w => w.Name, data.Name)
            .SetProperty(w => w.Description, data.Description)
            .SetProperty(w => w.Contact, data.Contact)
            .SetProperty(w => w.Telephone, data.Telephone)
            .SetProperty(w => w.City, data.City)
            .SetProperty(w => w.Address, data.Address)
            .SetProperty(w => w.Type, data.Type), ct) > 0;

    public async Task<bool> DeleteAsync(int id, CancellationToken ct) =>
        await db.DdWarehouses.Where(w => w.WarehouseId == id).ExecuteDeleteAsync(ct) > 0;

    public async Task<IReadOnlySet<int>> ExistingIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        (await db.DdWarehouses.Where(w => ids.Contains(w.WarehouseId)).Select(w => w.WarehouseId).ToListAsync(ct)).ToHashSet();

    private static LocationDto ToDto(DdWarehouse w) => new(w.WarehouseId, w.Name, w.Description, w.Contact, w.Telephone, w.City, w.Address, w.Type);

    private static void Apply(DdWarehouse w, ShopLocationData d) =>
        (w.Name, w.Description, w.Contact, w.Telephone, w.City, w.Address, w.Type) = (d.Name, d.Description, d.Contact, d.Telephone, d.City, d.Address, d.Type);
}
