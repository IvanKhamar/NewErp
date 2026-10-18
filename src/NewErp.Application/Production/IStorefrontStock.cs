namespace NewErp.Application.Production;

/// <summary>
/// Передає пошиті залишки на вітрину магазину (OpenCart), як pushStockToOc старого модуля StockTrack:
/// кількість кожного зв'язаного розміру товару = пошитий залишок розміру моделі, кількість товару = їхня сума.
/// </summary>
public interface IStorefrontStock
{
    /// <param name="productionIds">Моделі, залишки яких передати; null — усі активні.</param>
    Task<StorefrontSyncResult> PushAsync(IReadOnlyCollection<int>? productionIds, CancellationToken ct);
}

/// <summary>Models — скільки моделей передано, Products — скільки товарів магазину оновлено.</summary>
public sealed record StorefrontSyncResult(int Models, int Products);
