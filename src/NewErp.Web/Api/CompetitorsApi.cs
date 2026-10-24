using System.Net.Http.Json;

using NewErp.Application.Competitors;

namespace NewErp.Web.Api;

/// <summary>Виклики API цін конкурентів (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class CompetitorsApi(HttpClient http)
{
    public Task<List<CompetitorDto>> CompetitorsAsync() => GetAsync<List<CompetitorDto>>("/competitors");

    public async Task<CompetitorDto> CreateAsync(SaveCompetitorRequest request) =>
        await ReadAsync<CompetitorDto>(await http.PostAsJsonAsync("/competitors", request));

    public async Task<CompetitorDto> UpdateAsync(int id, SaveCompetitorRequest request) =>
        await ReadAsync<CompetitorDto>(await http.PutAsJsonAsync($"/competitors/{id}", request));

    public Task<List<CompetitorLinkDto>> LinksAsync() => GetAsync<List<CompetitorLinkDto>>("/competitors/links");

    public async Task<CompetitorLinkDto> AddLinkAsync(AddCompetitorLinkRequest request) =>
        await ReadAsync<CompetitorLinkDto>(await http.PostAsJsonAsync("/competitors/links", request));

    public async Task DeleteLinkAsync(int id) => await EnsureAsync(await http.DeleteAsync($"/competitors/links/{id}"));

    public async Task<CompetitorLinkDto> SetPriceAsync(int id, decimal price) =>
        await ReadAsync<CompetitorLinkDto>(await http.PostAsJsonAsync($"/competitors/links/{id}/price", new CompetitorPriceRequest(price)));

    public async Task<CompetitorLinkDto> CheckAsync(int id) =>
        await ReadAsync<CompetitorLinkDto>(await http.PostAsync($"/competitors/links/{id}/check", null));

    public Task<List<CompetitorPriceDto>> HistoryAsync(int id) => GetAsync<List<CompetitorPriceDto>>($"/competitors/links/{id}/history");

    public async Task<CompetitorCheckResult> CheckAllAsync() =>
        await ReadAsync<CompetitorCheckResult>(await http.PostAsync("/competitors/check", null));

    public Task<CompetitorReportDto> ReportAsync() => GetAsync<CompetitorReportDto>("/competitors/report");

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
