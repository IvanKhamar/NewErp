using System.Net.Http.Json;

using NewErp.Application.Catalog;

namespace NewErp.Web.Api;

/// <summary>Виклики API передзамовлень (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class PreOrdersApi(HttpClient http)
{
    public Task<List<PreOrderRow>> ListAsync(string? search, bool onlyWithQuota) =>
        GetAsync<List<PreOrderRow>>($"/preorders?search={Uri.EscapeDataString(search ?? "")}&onlyWithQuota={(onlyWithQuota ? "true" : "false")}");

    public async Task UpdateAsync(int productOptionValueId, UpdatePreOrderRequest request) =>
        await EnsureAsync(await http.PutAsJsonAsync($"/preorders/{productOptionValueId}", request));

    public async Task<PreOrderSettleResult> SettleAsync() => await ReadAsync<PreOrderSettleResult>(await http.PostAsync("/preorders/settle", null));

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
