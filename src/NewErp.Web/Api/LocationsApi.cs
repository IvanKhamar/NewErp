using System.Net.Http.Headers;
using System.Net.Http.Json;

using NewErp.Application.Locations;

namespace NewErp.Web.Api;

/// <summary>Виклики API довідника складів і пунктів видачі (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class LocationsApi(HttpClient http)
{
    /// <summary>Вивантаження відкривається звичайним посиланням: браузер сам збереже файл, cookie входу йде разом із запитом.</summary>
    public const string ExportUrl = "/locations/export.xlsx";

    private sealed record Created(int Id);

    public Task<List<LocationDto>> ListAsync(string? search) =>
        GetAsync<List<LocationDto>>(string.IsNullOrWhiteSpace(search) ? "/locations" : $"/locations?search={Uri.EscapeDataString(search.Trim())}");

    public async Task<int> CreateAsync(SaveLocationRequest request) =>
        (await ReadAsync<Created>(await http.PostAsJsonAsync("/locations", request))).Id;

    public async Task UpdateAsync(int id, SaveLocationRequest request) =>
        await EnsureAsync(await http.PutAsJsonAsync($"/locations/{id}", request));

    public async Task DeleteAsync(int id) => await EnsureAsync(await http.DeleteAsync($"/locations/{id}"));

    public async Task<LocationImportResult> ImportAsync(Stream file)
    {
        var content = new StreamContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return await ReadAsync<LocationImportResult>(await http.PostAsync("/locations/import", content));
    }

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
