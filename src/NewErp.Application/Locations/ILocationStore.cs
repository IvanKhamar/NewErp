using NewErp.Domain.Locations;

namespace NewErp.Application.Locations;

public interface ILocationStore
{
    Task<IReadOnlyList<LocationDto>> ListAsync(string? search, CancellationToken ct);

    Task<LocationDto?> GetAsync(int id, CancellationToken ct);

    Task<int> AddAsync(ShopLocationData data, CancellationToken ct);

    /// <summary>false, якщо запису немає.</summary>
    Task<bool> UpdateAsync(int id, ShopLocationData data, CancellationToken ct);

    Task<bool> DeleteAsync(int id, CancellationToken ct);

    Task<IReadOnlySet<int>> ExistingIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
}

/// <summary>Читання й запис файлу Excel (.xlsx) з довідником.</summary>
public interface ILocationSpreadsheet
{
    byte[] Write(IReadOnlyList<string> header, IReadOnlyList<LocationDto> rows);

    /// <summary>Рядки першого аркуша як текст; DomainException, якщо це не .xlsx.</summary>
    IReadOnlyList<IReadOnlyList<string>> Read(Stream file);
}
