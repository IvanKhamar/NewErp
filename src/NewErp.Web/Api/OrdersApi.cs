using System.Globalization;
using System.Net.Http.Json;

using NewErp.Application.Common;
using NewErp.Application.Delivery;
using NewErp.Application.Orders;
using NewErp.Application.Sms;

namespace NewErp.Web.Api;

/// <summary>Виклики API замовлень і SMS (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class OrdersApi(HttpClient http)
{
    public Task<PagedResult<OrderListItem>> ListAsync(OrderFilter filter)
    {
        var query = new List<string> { $"page={filter.Page}", $"pageSize={filter.PageSize}" };
        if (filter.StatusId is { } status) query.Add($"statusId={status}");
        if (filter.SourceId is { } source) query.Add($"sourceId={source}");
        if (filter.ManagerId is { } manager) query.Add($"managerId={manager}");
        if (filter.From is { } from) query.Add($"from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (filter.To is { } to) query.Add($"to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(filter.Search)) query.Add($"search={Uri.EscapeDataString(filter.Search.Trim())}");
        return GetAsync<PagedResult<OrderListItem>>("/orders?" + string.Join('&', query));
    }

    public Task<OrderDetails> GetAsync(int id) => GetAsync<OrderDetails>($"/orders/{id}");

    public Task<List<OrderStatusDto>> StatusesAsync() => GetAsync<List<OrderStatusDto>>("/orders/statuses");

    public Task<List<LookupItem>> SourcesAsync() => GetAsync<List<LookupItem>>("/orders/sources");

    public Task<List<LookupItem>> ManagersAsync() => GetAsync<List<LookupItem>>("/orders/managers");

    public async Task<ChangeOrderStatusResult> ChangeStatusAsync(int id, int statusId) =>
        await ReadAsync<ChangeOrderStatusResult>(await http.PutAsJsonAsync($"/orders/{id}/status", new ChangeOrderStatusRequest(statusId)));

    public async Task AssignManagerAsync(int id, int managerId) =>
        await EnsureAsync(await http.PutAsJsonAsync($"/orders/{id}/manager", new AssignManagerRequest(managerId)));

    public Task<List<SmsDto>> SmsAsync(int id) => GetAsync<List<SmsDto>>($"/orders/{id}/sms");

    /// <param name="force">Надіслати, навіть якщо номер ТТН у тексті належить іншому замовленню.</param>
    public async Task<SendSmsResultDto> SendSmsAsync(int id, string text, bool force = false) =>
        await ReadAsync<SendSmsResultDto>(await http.PostAsJsonAsync($"/orders/{id}/sms", new SendSmsRequest(text, force)));

    /// <returns>ТТН замовлення або null, якщо її ще немає.</returns>
    public async Task<ShipmentDto?> ShipmentAsync(int id)
    {
        var response = await http.GetAsync($"/orders/{id}/shipment");
        return response.StatusCode == System.Net.HttpStatusCode.NoContent ? null : await ReadAsync<ShipmentDto>(response);
    }

    public async Task<TrackingRefreshResult> RefreshTrackingAsync() =>
        await ReadAsync<TrackingRefreshResult>(await http.PostAsync("/delivery/tracking/refresh", null));

    public Task<List<ReminderDto>> RemindersAsync() => GetAsync<List<ReminderDto>>("/delivery/reminders");

    public async Task MarkRemindedAsync(int orderId) => await EnsureAsync(await http.PostAsync($"/delivery/reminders/{orderId}/done", null));

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
