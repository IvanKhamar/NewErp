using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Locations;

namespace NewErp.Application.Locations;

/// <summary>Довідник складів і пунктів видачі (dd_warehouse) з імпортом і вивантаженням Excel, як «Каталог → Склади» старої адмінки.</summary>
public sealed class LocationService(ILocationStore store, ILocationSpreadsheet spreadsheet)
{
    public const int MaxImportBytes = 5 * 1024 * 1024;

    public Task<IReadOnlyList<LocationDto>> ListAsync(string? search, CancellationToken ct) => store.ListAsync(search?.Trim(), ct);

    public async Task<LocationDto> GetAsync(int id, CancellationToken ct) =>
        await store.GetAsync(id, ct) ?? throw new NotFoundException($"Склад {id} не знайдено.");

    public Task<int> CreateAsync(SaveLocationRequest request, CancellationToken ct) => store.AddAsync(Validate(request), ct);

    public async Task UpdateAsync(int id, SaveLocationRequest request, CancellationToken ct)
    {
        if (!await store.UpdateAsync(id, Validate(request), ct))
            throw new NotFoundException($"Склад {id} не знайдено.");
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        if (!await store.DeleteAsync(id, ct))
            throw new NotFoundException($"Склад {id} не знайдено.");
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct) => spreadsheet.Write(ShopLocation.Columns, await store.ListAsync(null, ct));

    /// <summary>Рядок з ID наявного запису оновлює його, решта додаються (як старий імпорт). Із помилками нічого не записується.</summary>
    public async Task<LocationImportResult> ImportAsync(Stream file, CancellationToken ct)
    {
        var (rows, errors) = ShopLocation.ParseImport(spreadsheet.Read(file));
        if (errors.Count > 0)
            return new LocationImportResult(0, 0, errors);

        var existing = await store.ExistingIdsAsync(rows.Where(r => r.Id > 0).Select(r => r.Id).ToList(), ct);
        var (added, updated) = (0, 0);
        foreach (var row in rows)
        {
            if (existing.Contains(row.Id))
            {
                await store.UpdateAsync(row.Id, row.Data, ct);
                updated++;
            }
            else
            {
                // ID, якого в базі немає, не відтворюємо: запис отримує новий номер, як і в старому імпорті.
                await store.AddAsync(row.Data, ct);
                added++;
            }
        }
        return new LocationImportResult(added, updated, []);
    }

    private static ShopLocationData Validate(SaveLocationRequest r) =>
        ShopLocation.Validate(r.Name, r.Description, r.Contact, r.Telephone, r.City, r.Address, r.Type);
}
