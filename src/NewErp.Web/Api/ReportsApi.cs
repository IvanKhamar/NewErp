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

    /// <summary>Статус і групування фільтра тут не діють: звіт показує кожен статус окремо.</summary>
    public Task<StatusReportDto> StatusesAsync(ReportFilter filter) =>
        GetAsync<StatusReportDto>("/reports/statuses?" + Query(filter with { StatusId = null }, withGroup: false));

    public Task<UtmReportDto> UtmAsync(ReportFilter filter, UtmFilter utm)
    {
        var query = new List<string> { Query(filter, withGroup: true), $"bySource={(utm.BySource ? "true" : "false")}" };
        if (!string.IsNullOrEmpty(utm.Source)) query.Add($"source={Uri.EscapeDataString(utm.Source)}");
        if (!string.IsNullOrEmpty(utm.Medium)) query.Add($"medium={Uri.EscapeDataString(utm.Medium)}");
        if (!string.IsNullOrEmpty(utm.Campaign)) query.Add($"campaign={Uri.EscapeDataString(utm.Campaign)}");
        return GetAsync<UtmReportDto>("/reports/utm?" + string.Join('&', query));
    }

    public Task<IReadOnlyList<OnlineVisitorDto>> OnlineAsync() => GetAsync<IReadOnlyList<OnlineVisitorDto>>("/reports/online");

    public Task<IReadOnlyList<ProductViewsDto>> ProductViewsAsync() => GetAsync<IReadOnlyList<ProductViewsDto>>("/reports/product-views");

    public Task<IReadOnlyList<CustomerRewardDto>> RewardsAsync() => GetAsync<IReadOnlyList<CustomerRewardDto>>("/reports/rewards");

    public Task<TaxReportDto> TaxesAsync(ReportFilter filter) => GetAsync<TaxReportDto>("/reports/taxes?" + Query(filter, withGroup: true));

    public Task<StandardReportDto> StandardAsync(string kind, DateOnly? from, DateOnly? to, string group)
    {
        var query = new List<string> { $"group={group}" };
        if (from is { } start) query.Add($"from={start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (to is { } end) query.Add($"to={end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        return GetAsync<StandardReportDto>($"/reports/standard/{kind}?{string.Join('&', query)}");
    }

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
