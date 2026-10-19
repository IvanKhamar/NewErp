using System.Net.Http.Json;

using NewErp.Application.Common;
using NewErp.Application.Returns;

namespace NewErp.Web.Api;

/// <summary>Виклики API повернень (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class ReturnsApi(HttpClient http)
{
    private sealed record Created(int Id);

    public Task<PagedResult<ReturnListItem>> ListAsync(ReturnFilter filter)
    {
        var query = new List<string> { $"page={filter.Page}", $"pageSize={filter.PageSize}" };
        if (filter.OrderId is { } order) query.Add($"orderId={order}");
        if (filter.StatusId is { } status) query.Add($"statusId={status}");
        if (!string.IsNullOrWhiteSpace(filter.Search)) query.Add($"search={Uri.EscapeDataString(filter.Search.Trim())}");
        return GetAsync<PagedResult<ReturnListItem>>("/returns?" + string.Join('&', query));
    }

    public Task<ReturnDetails> GetAsync(int id) => GetAsync<ReturnDetails>($"/returns/{id}");

    public Task<ReturnLookupsDto> LookupsAsync() => GetAsync<ReturnLookupsDto>("/returns/lookups");

    public async Task<int> CreateAsync(CreateReturnRequest request) =>
        (await ReadAsync<Created>(await http.PostAsJsonAsync("/returns", request))).Id;

    public async Task AddHistoryAsync(int id, AddReturnHistoryRequest request) =>
        await EnsureAsync(await http.PostAsJsonAsync($"/returns/{id}/history", request));

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
