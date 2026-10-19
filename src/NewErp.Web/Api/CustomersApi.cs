using System.Net.Http.Json;

using NewErp.Application.Common;
using NewErp.Application.Customers;

namespace NewErp.Web.Api;

/// <summary>Виклики API покупців (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class CustomersApi(HttpClient http)
{
    public Task<PagedResult<CustomerListItem>> ListAsync(CustomerFilter filter)
    {
        var query = new List<string> { $"page={filter.Page}", $"pageSize={filter.PageSize}" };
        if (filter.GroupId is { } group) query.Add($"groupId={group}");
        if (filter.Status is { } status) query.Add($"status={(status ? "true" : "false")}");
        if (!string.IsNullOrWhiteSpace(filter.Search)) query.Add($"search={Uri.EscapeDataString(filter.Search.Trim())}");
        return GetAsync<PagedResult<CustomerListItem>>("/customers?" + string.Join('&', query));
    }

    public Task<IReadOnlyList<CustomerGroupDto>> GroupsAsync() => GetAsync<IReadOnlyList<CustomerGroupDto>>("/customers/groups");

    public Task<CustomerDetails> GetAsync(int id) => GetAsync<CustomerDetails>($"/customers/{id}");

    public async Task AddNoteAsync(int id, AddCustomerNoteRequest request) =>
        await EnsureAsync(await http.PostAsJsonAsync($"/customers/{id}/notes", request));

    private async Task<T> GetAsync<T>(string url)
    {
        var response = await http.GetAsync(url);
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ApiErrors.ReadAsync(response));
    }
}
