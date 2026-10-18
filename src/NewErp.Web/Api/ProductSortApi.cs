using System.Globalization;
using System.Net.Http.Json;

using NewErp.Application.Catalog;
using NewErp.Domain.Catalog;

namespace NewErp.Web.Api;

/// <summary>Виклики API порядку товарів (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class ProductSortApi(HttpClient http)
{
    public Task<List<SortGroupDto>> GroupsAsync() => GetAsync<List<SortGroupDto>>("/catalog/sort/groups");

    public Task<List<SortPreviewRow>> PreviewAsync(SortGroupType type, string key, SalesPeriod period) =>
        GetAsync<List<SortPreviewRow>>($"/catalog/sort/preview?type={type}&key={Uri.EscapeDataString(key)}{PeriodQuery(period)}");

    public async Task<SortResult> ApplyAsync(ApplySortRequest request) =>
        await ReadAsync<SortResult>(await http.PostAsJsonAsync("/catalog/sort/apply", request));

    public async Task<SortResult> RecalculateAllAsync(SalesPeriod period) =>
        await ReadAsync<SortResult>(await http.PostAsJsonAsync("/catalog/sort/recalculate-all", period));

    public Task<List<SortSnapshotDto>> SnapshotsAsync() => GetAsync<List<SortSnapshotDto>>("/catalog/sort/snapshots");

    public async Task RestoreSnapshotAsync(int id) => await EnsureAsync(await http.PostAsync($"/catalog/sort/snapshots/{id}/restore", null));

    public Task<List<SortPinDto>> PinsAsync() => GetAsync<List<SortPinDto>>("/catalog/sort/pins");

    public async Task SetPinAsync(SetPinRequest request) => await EnsureAsync(await http.PostAsJsonAsync("/catalog/sort/pins", request));

    public async Task RemovePinAsync(int id) => await EnsureAsync(await http.DeleteAsync($"/catalog/sort/pins/{id}"));

    public Task<CrossSellMetricDto> CrossSellMetricAsync() => GetAsync<CrossSellMetricDto>("/catalog/sort/crosssell-metric");

    public async Task SetCrossSellMetricAsync(bool panelOnly) =>
        await EnsureAsync(await http.PutAsJsonAsync("/catalog/sort/crosssell-metric", new CrossSellMetricDto(panelOnly)));

    public Task<List<SortProductDto>> ProductsAsync(string search) =>
        GetAsync<List<SortProductDto>>($"/catalog/sort/products?search={Uri.EscapeDataString(search)}");

    private static string PeriodQuery(SalesPeriod period) =>
        period is { From: { } from, To: { } to }
            ? $"&from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
            : period.Days is { } days ? $"&days={days}" : "";

    private async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await http.GetAsync(url));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ApiErrors.ReadAsync(response));
    }
}
