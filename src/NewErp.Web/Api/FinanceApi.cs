using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Cashbox;
using NewErp.Application.Common;
using NewErp.Application.Payments;

namespace NewErp.Web.Api;

/// <summary>Виклики API каси Checkbox і банківських надходжень (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class FinanceApi(HttpClient http)
{
    // ─── Каса ───

    public Task<CashboxSetupDto> SetupAsync() => GetAsync<CashboxSetupDto>("/cashbox/setup");

    public async Task SaveCashierAsync(int? id, SaveCashierRequest request) =>
        await EnsureAsync(id is { } existing
            ? await http.PutAsJsonAsync($"/cashbox/cashiers/{existing}", request)
            : await http.PostAsJsonAsync("/cashbox/cashiers", request));

    /// <param name="kind">warehouses або requisites.</param>
    public async Task SaveEntryAsync(string kind, int? id, SaveDirectoryEntryRequest request) =>
        await EnsureAsync(id is { } existing
            ? await http.PutAsJsonAsync($"/cashbox/{kind}/{existing}", request)
            : await http.PostAsJsonAsync($"/cashbox/{kind}", request));

    public async Task DeleteEntryAsync(string kind, int id) => await EnsureAsync(await http.DeleteAsync($"/cashbox/{kind}/{id}"));

    public async Task<WorkShiftDto> OpenShiftAsync(int cashierId) =>
        await ReadAsync<WorkShiftDto>(await http.PostAsync($"/cashbox/cashiers/{cashierId}/shift/open", null));

    /// <returns>Z-звіт або null, якщо Checkbox ще закриває зміну.</returns>
    public async Task<ZReportDto?> CloseShiftAsync(int cashierId)
    {
        var response = await http.PostAsync($"/cashbox/cashiers/{cashierId}/shift/close", null);
        return response.StatusCode == HttpStatusCode.Accepted ? null : await ReadAsync<ZReportDto>(response);
    }

    /// <returns>Z-звіт або null, якщо зміна ще відкрита в Checkbox.</returns>
    public async Task<ZReportDto?> SyncShiftAsync(int shiftId)
    {
        var response = await http.PostAsync($"/cashbox/shifts/{shiftId}/sync", null);
        return response.StatusCode == HttpStatusCode.NoContent ? null : await ReadAsync<ZReportDto>(response);
    }

    public async Task<ReportTextDto> XReportAsync(int cashierId) =>
        await ReadAsync<ReportTextDto>(await http.PostAsync($"/cashbox/cashiers/{cashierId}/x-report", null));

    public Task<ReportTextDto> ZReportTextAsync(int reportId) => GetAsync<ReportTextDto>($"/cashbox/reports/{reportId}/text");

    public Task<List<WorkShiftDto>> ShiftsAsync() => GetAsync<List<WorkShiftDto>>("/cashbox/shifts");

    public Task<List<ZReportDto>> ReportsAsync() => GetAsync<List<ZReportDto>>("/cashbox/reports");

    public Task<PagedResult<ChequeDto>> ChequesAsync(ChequeFilter filter)
    {
        var query = new List<string> { $"page={filter.Page}", $"pageSize={filter.PageSize}" };
        if (filter.CashierId is { } cashier) query.Add($"cashierId={cashier}");
        if (filter.OrderId is { } order) query.Add($"orderId={order}");
        if (filter.Fiscalized is { } fiscalized) query.Add($"fiscalized={fiscalized.ToString().ToLowerInvariant()}");
        if (filter.From is { } from) query.Add($"from={Day(from)}");
        if (filter.To is { } to) query.Add($"to={Day(to)}");
        return GetAsync<PagedResult<ChequeDto>>("/cashbox/cheques?" + string.Join('&', query));
    }

    public async Task<ChequeDto> FiscalizeAsync(int chequeId) =>
        await ReadAsync<ChequeDto>(await http.PostAsync($"/cashbox/cheques/{chequeId}/fiscalize", null));

    public async Task DeleteChequeAsync(int chequeId) => await EnsureAsync(await http.DeleteAsync($"/cashbox/cheques/{chequeId}"));

    /// <returns>Чек замовлення або null, якщо його ще немає.</returns>
    public async Task<ChequeDto?> OrderChequeAsync(int orderId)
    {
        var response = await http.GetAsync($"/orders/{orderId}/cheque");
        return response.StatusCode == HttpStatusCode.NoContent ? null : await ReadAsync<ChequeDto>(response);
    }

    public async Task<ChequeDto> IssueChequeAsync(int orderId, int? cashierId) =>
        await ReadAsync<ChequeDto>(await http.PostAsJsonAsync($"/orders/{orderId}/cheque", new IssueChequeRequest(cashierId)));

    // ─── Банк ───

    public Task<PagedResult<BankPaymentDto>> PaymentsAsync(BankPaymentFilter filter)
    {
        var query = new List<string> { $"page={filter.Page}", $"pageSize={filter.PageSize}", $"hidden={filter.Hidden.ToString().ToLowerInvariant()}" };
        if (filter.Linked is { } linked) query.Add($"linked={linked.ToString().ToLowerInvariant()}");
        if (filter.From is { } from) query.Add($"from={Day(from)}");
        if (filter.To is { } to) query.Add($"to={Day(to)}");
        if (!string.IsNullOrWhiteSpace(filter.Search)) query.Add($"search={Uri.EscapeDataString(filter.Search.Trim())}");
        return GetAsync<PagedResult<BankPaymentDto>>("/payments/bank?" + string.Join('&', query));
    }

    public async Task<BankImportResult> ImportAsync() => await ReadAsync<BankImportResult>(await http.PostAsync("/payments/bank/import", null));

    public Task<List<PaymentSuggestionDto>> SuggestionsAsync(int paymentId, string? customer) =>
        GetAsync<List<PaymentSuggestionDto>>($"/payments/bank/{paymentId}/suggestions" +
            (string.IsNullOrWhiteSpace(customer) ? "" : $"?customer={Uri.EscapeDataString(customer.Trim())}"));

    public async Task<BankPaymentDto> LinkAsync(int paymentId, int orderId) =>
        await ReadAsync<BankPaymentDto>(await http.PutAsJsonAsync($"/payments/bank/{paymentId}/order", new LinkPaymentRequest(orderId)));

    public async Task SetVisibleAsync(int paymentId, bool visible) =>
        await EnsureAsync(await http.PutAsync($"/payments/bank/{paymentId}/visible?visible={visible.ToString().ToLowerInvariant()}", null));

    public Task<List<BankPaymentDto>> OrderPaymentsAsync(int orderId) => GetAsync<List<BankPaymentDto>>($"/orders/{orderId}/payments");

    private static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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
