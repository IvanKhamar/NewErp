using System.Net.Http.Json;

using NewErp.Application.Catalog;
using NewErp.Application.Common;

namespace NewErp.Web.Api;

/// <summary>Виклики API товарів магазину (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class ProductsApi(HttpClient http)
{
    public Task<PagedResult<ProductListItem>> ListAsync(ProductFilter filter)
    {
        var query = new List<string> { $"page={filter.Page}", $"pageSize={filter.PageSize}" };
        if (filter.CategoryId is { } category) query.Add($"categoryId={category}");
        if (filter.LabelId is { } label) query.Add($"labelId={label}");
        if (filter.Enabled is { } enabled) query.Add($"enabled={(enabled ? "true" : "false")}");
        if (!string.IsNullOrWhiteSpace(filter.Search)) query.Add($"search={Uri.EscapeDataString(filter.Search.Trim())}");
        return GetAsync<PagedResult<ProductListItem>>("/catalog/products?" + string.Join('&', query));
    }

    public Task<ProductCardDto> GetAsync(int id) => GetAsync<ProductCardDto>($"/catalog/products/{id}");

    public async Task<ProductCardDto> UpdateAsync(int id, UpdateProductRequest request)
    {
        var response = await http.PatchAsJsonAsync($"/catalog/products/{id}", request);
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<ProductCardDto>())!;
    }

    public Task<IReadOnlyList<LookupItem>> LabelsAsync() => GetAsync<IReadOnlyList<LookupItem>>("/catalog/products/labels");

    public Task<IReadOnlyList<LookupItem>> CategoriesAsync() => GetAsync<IReadOnlyList<LookupItem>>("/catalog/products/categories");

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
