using System.Globalization;
using System.Net.Http.Json;

using NewErp.Application.Reports;

namespace NewErp.Web.Api;

/// <summary>Виклики API звітів (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class ReportsApi(HttpClient http)
{
    public Task<DashboardDto> DashboardAsync() => GetAsync<DashboardDto>("/reports/dashboard");

    public Task<SalesReportDto> SalesAsync(ReportFilter filter) => GetAsync<SalesReportDto>("/reports/sales?" + Query(filter, withGroup: true));

    public Task<ProductSalesDto> ProductsAsync(ReportFilter filter) => GetAsync<ProductSalesDto>("/reports/products?" + Query(filter, withGroup: false));

    private static string Query(ReportFilter filter, bool withGroup)
    {
        var query = new List<string>();
        if (filter.From is { } from) query.Add($"from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (filter.To is { } to) query.Add($"to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (filter.StatusId is { } status) query.Add($"statusId={status}");
        if (withGroup) query.Add($"group={filter.Group}");
        return string.Join('&', query);
    }

    private async Task<T> GetAsync<T>(string url)
    {
        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ApiErrors.ReadAsync(response));
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
