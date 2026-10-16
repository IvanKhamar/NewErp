using NewErp.Domain.Delivery;

namespace NewErp.Application.Delivery;

/// <summary>Довідник міст і відділень Нової Пошти (локальна копія, спільна зі старим сайтом).</summary>
public interface IWarehouseDirectory
{
    /// <summary>Міста, назва яких починається з рядка пошуку.</summary>
    Task<IReadOnlyList<CityDto>> SearchCitiesAsync(string search, int limit, CancellationToken ct);

    /// <summary>Робочі відділення міста, у назві яких є рядок пошуку (порожній — усі), за номером.</summary>
    Task<IReadOnlyList<WarehouseDto>> SearchWarehousesAsync(string cityRef, string search, int limit, CancellationToken ct);

    /// <summary>Місто за точною назвою (як записано в замовленні).</summary>
    Task<CityDto?> FindCityAsync(string name, CancellationToken ct);

    /// <summary>Відділення міста за точною назвою (як записано в замовленні).</summary>
    Task<WarehouseDto?> FindWarehouseAsync(string cityRef, string name, CancellationToken ct);

    Task<bool> WarehouseExistsAsync(string cityRef, string warehouseRef, CancellationToken ct);

    /// <summary>Замінює весь довідник одним записом.</summary>
    Task ReplaceAllAsync(IReadOnlyList<NovaPoshtaWarehouse> warehouses, CancellationToken ct);
}
