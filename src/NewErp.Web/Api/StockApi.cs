using System.Globalization;
using System.Net.Http.Json;

using NewErp.Application.Common;
using NewErp.Application.Stock;

namespace NewErp.Web.Api;

/// <summary>
/// Виклики API складу. Сторінки працюють через цей клас, а не з HttpClient напряму:
/// адреси в одному місці, а відмова API стає <see cref="ApiException"/> з поясненням для людини.
/// </summary>
public sealed class StockApi(HttpClient http)
{
    private sealed record Created(int Id);

    public Task<List<LookupItem>> CategoriesAsync() => GetAsync<List<LookupItem>>("/stock/categories");

    public Task<int> CreateCategoryAsync(CreateStockCategoryRequest request) => CreateAsync("/stock/categories", request);

    public Task<List<StockSupplierDto>> SuppliersAsync() => GetAsync<List<StockSupplierDto>>("/stock/suppliers");

    public Task<int> CreateSupplierAsync(CreateStockSupplierRequest request) => CreateAsync("/stock/suppliers", request);

    public Task<List<StockItemDto>> ItemsAsync(int? categoryId = null, bool belowMinimumOnly = false)
    {
        var query = new List<string>();
        if (categoryId is { } id)
            query.Add($"categoryId={id}");
        if (belowMinimumOnly)
            query.Add("belowMinimumOnly=true");
        return GetAsync<List<StockItemDto>>("/stock/items" + (query.Count > 0 ? "?" + string.Join('&', query) : ""));
    }

    public Task<StockItemDto> ItemAsync(int id) => GetAsync<StockItemDto>($"/stock/items/{id}");

    public Task<int> CreateItemAsync(CreateStockItemRequest request) => CreateAsync("/stock/items", request);

    public async Task<StockMovementDto> AdjustAsync(int itemId, AdjustStockRequest request) =>
        await ReadAsync<StockMovementDto>(await http.PostAsJsonAsync($"/stock/items/{itemId}/adjustments", request));

    public async Task SetMinQuantityAsync(int itemId, decimal minQuantity) =>
        await EnsureAsync(await http.PutAsync(
            $"/stock/items/{itemId}/min-quantity?minQuantity={minQuantity.ToString(CultureInfo.InvariantCulture)}", null));

    public Task<List<StockMovementDto>> MovementsAsync(int itemId) => GetAsync<List<StockMovementDto>>($"/stock/items/{itemId}/movements");

    public Task<ReorderReportDto> ReorderAsync() => GetAsync<ReorderReportDto>("/stock/reorder");

    public Task<List<PurchaseOrderSummaryDto>> PurchaseOrdersAsync(string? status = null) =>
        GetAsync<List<PurchaseOrderSummaryDto>>("/stock/purchase-orders" + (status is null ? "" : $"?status={status}"));

    public Task<PurchaseOrderDto> PurchaseOrderAsync(int id) => GetAsync<PurchaseOrderDto>($"/stock/purchase-orders/{id}");

    public Task<int> CreatePurchaseOrderFromReorderAsync(int supplierId) =>
        CreateAsync<object>($"/stock/purchase-orders/from-reorder/{supplierId}", new { });

    public async Task SetPurchaseOrderLinesAsync(int id, SetPurchaseOrderLinesRequest request) =>
        await EnsureAsync(await http.PutAsJsonAsync($"/stock/purchase-orders/{id}/lines", request));

    public async Task MarkOrderedAsync(int id) => await EnsureAsync(await http.PostAsync($"/stock/purchase-orders/{id}/ordered", null));

    public async Task ReceiveAsync(int id, ReceivePurchaseOrderRequest request) =>
        await EnsureAsync(await http.PostAsJsonAsync($"/stock/purchase-orders/{id}/receive", request));

    public async Task CancelPurchaseOrderAsync(int id) => await EnsureAsync(await http.PostAsync($"/stock/purchase-orders/{id}/cancel", null));

    private async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await http.GetAsync(url));

    private async Task<int> CreateAsync<T>(string url, T body) =>
        (await ReadAsync<Created>(await http.PostAsJsonAsync(url, body))).Id;

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
