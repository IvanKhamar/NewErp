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

    public Task<ShipmentFormDto> ShipmentFormAsync(int id) => GetAsync<ShipmentFormDto>($"/orders/{id}/shipment/form");

    public async Task<ShipmentDto> CreateShipmentAsync(int id, CreateShipmentRequest request) =>
        await ReadAsync<ShipmentDto>(await http.PostAsJsonAsync($"/orders/{id}/shipment", request));

    public async Task DeleteShipmentAsync(int id) => await EnsureAsync(await http.DeleteAsync($"/orders/{id}/shipment"));

    /// <summary>Адреса PDF для друку: відкривається в новій вкладці, cookie входу браузер додасть сам.</summary>
    public static string ShipmentPrintUrl(int id, ShipmentPrintKind kind) => $"/orders/{id}/shipment/print?kind={kind}";

    public Task<List<CityDto>> CitiesAsync(string search) => GetAsync<List<CityDto>>($"/delivery/cities?search={Uri.EscapeDataString(search)}");

    public Task<List<WarehouseDto>> WarehousesAsync(string cityRef, string search) =>
        GetAsync<List<WarehouseDto>>($"/delivery/warehouses?cityRef={Uri.EscapeDataString(cityRef)}&search={Uri.EscapeDataString(search)}");

    public async Task<WarehouseRefreshResult> RefreshWarehousesAsync() =>
        await ReadAsync<WarehouseRefreshResult>(await http.PostAsync("/delivery/warehouses/refresh", null));

    public async Task<CreateOrderResult> CreateAsync(CreateOrderRequest request) =>
        await ReadAsync<CreateOrderResult>(await http.PostAsJsonAsync("/orders", request));

    public Task<OrderMethodsDto> MethodsAsync() => GetAsync<OrderMethodsDto>("/orders/new/methods");

    public Task<List<ProductPickDto>> ProductsAsync(string search) =>
        GetAsync<List<ProductPickDto>>($"/orders/new/products?search={Uri.EscapeDataString(search)}");

    public Task<List<CustomerPickDto>> CustomersAsync(string search) =>
        GetAsync<List<CustomerPickDto>>($"/orders/new/customers?search={Uri.EscapeDataString(search)}");

    public Task<CustomerRiskDto> CustomerRiskAsync(string telephone, string firstname, string lastname, string email) =>
        GetAsync<CustomerRiskDto>($"/orders/customer-risk?telephone={Uri.EscapeDataString(telephone)}&firstname={Uri.EscapeDataString(firstname)}" +
            $"&lastname={Uri.EscapeDataString(lastname)}&email={Uri.EscapeDataString(email)}");

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
