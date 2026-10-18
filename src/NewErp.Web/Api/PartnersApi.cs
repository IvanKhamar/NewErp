using System.Net.Http.Json;

using NewErp.Application.Partners;

namespace NewErp.Web.Api;

/// <summary>Виклики API партнерів-перепродавців (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class PartnersApi(HttpClient http)
{
    private sealed record Created(int Id);

    public Task<List<PartnerListItemDto>> ListAsync() => GetAsync<List<PartnerListItemDto>>("/partners");

    public Task<PartnerDto> GetAsync(int id) => GetAsync<PartnerDto>($"/partners/{id}");

    public async Task<int> CreateAsync(SavePartnerRequest request) =>
        (await ReadAsync<Created>(await http.PostAsJsonAsync("/partners", request))).Id;

    public async Task UpdateAsync(int id, SavePartnerRequest request) => await EnsureAsync(await http.PutAsJsonAsync($"/partners/{id}", request));

    public async Task DeleteAsync(int id) => await EnsureAsync(await http.DeleteAsync($"/partners/{id}"));

    public Task<List<ShopCategoryDto>> CategoriesAsync() => GetAsync<List<ShopCategoryDto>>("/partners/categories");

    public Task<List<PartnerProductPickDto>> ProductsAsync(string search) =>
        GetAsync<List<PartnerProductPickDto>>($"/partners/products?search={Uri.EscapeDataString(search)}");

    /// <summary>Адреса фіда відносно сервера (сторінки інтерфейсу живуть під /app/, а фід — у корені).</summary>
    public static string FeedPath(string code) => $"/feeds/{Uri.EscapeDataString(code)}.xml";

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
