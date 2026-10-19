using System.Net.Http.Json;

using NewErp.Application.Production;

namespace NewErp.Web.Api;

/// <summary>Виклики API виробництва (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class ProductionApi(HttpClient http)
{
    private sealed record Created(int Id);

    public Task<List<ProductionModelSummaryDto>> ModelsAsync() => GetAsync<List<ProductionModelSummaryDto>>("/production/models");

    public Task<ProductionModelDto> ModelAsync(int id) => GetAsync<ProductionModelDto>($"/production/models/{id}");

    public Task<int> CreateModelAsync(SaveProductionModelRequest request) => CreateAsync("/production/models", request);

    public Task UpdateModelAsync(int id, SaveProductionModelRequest request) => PutAsync($"/production/models/{id}", request);

    public async Task DeactivateModelAsync(int id) => await EnsureAsync(await http.DeleteAsync($"/production/models/{id}"));

    public Task SetLinksAsync(int id, SetProductionLinksRequest request) => PutAsync($"/production/models/{id}/links", request);

    public Task SetSewnBomAsync(int id, SetSewnBomRequest request) => PutAsync($"/production/models/{id}/sewn-bom", request);

    public Task<int> AddComponentAsync(int modelId, SaveComponentRequest request) => CreateAsync($"/production/models/{modelId}/components", request);

    public Task UpdateComponentAsync(int componentId, SaveComponentRequest request) => PutAsync($"/production/components/{componentId}", request);

    public async Task DeleteComponentAsync(int componentId) => await EnsureAsync(await http.DeleteAsync($"/production/components/{componentId}"));

    public Task SetComponentBomAsync(int componentId, SetComponentBomRequest request) => PutAsync($"/production/components/{componentId}/bom", request);

    /// <summary>Операція з залишками: шлях — cut, to-sewing, sewn або ship (див. <see cref="OperationRoute"/>).</summary>
    public async Task<ProductionOperationResult> RecordAsync(int modelId, ProductionOperation operation, ProductionOperationRequest request) =>
        await ReadAsync<ProductionOperationResult>(await http.PostAsJsonAsync($"/production/models/{modelId}/{OperationRoute(operation)}", request));

    public Task<SewingPlanDto> PlanAsync(bool onlyToSew) => GetAsync<SewingPlanDto>($"/production/plan?onlyToSew={(onlyToSew ? "true" : "false")}");

    public Task<List<ProductionLogDto>> LogAsync(int? modelId = null, int limit = 200) =>
        GetAsync<List<ProductionLogDto>>($"/production/log?limit={limit}" + (modelId is { } id ? $"&productionId={id}" : ""));

    public static string OperationRoute(ProductionOperation operation) => operation switch
    {
        ProductionOperation.Cut => "cut",
        ProductionOperation.SendToSewing => "to-sewing",
        ProductionOperation.Sewn => "sewn",
        ProductionOperation.Ship => "ship",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    public async Task<StorefrontSyncResult> SyncStorefrontAsync() =>
        await ReadAsync<StorefrontSyncResult>(await http.PostAsync("/production/storefront/sync", null));

    private async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await http.GetAsync(url));

    private async Task<int> CreateAsync<T>(string url, T body) => (await ReadAsync<Created>(await http.PostAsJsonAsync(url, body))).Id;

    private async Task PutAsync<T>(string url, T body) => await EnsureAsync(await http.PutAsJsonAsync(url, body));

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
