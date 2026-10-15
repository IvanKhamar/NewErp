using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Stock;

namespace NewErp.IntegrationTests;

public class ReorderApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Report_GroupsItemsBelowMinimumBySupplierWithSuggestedQuantity()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);
        var supplier = await CreateAsync(client, "/stock/suppliers", new CreateStockSupplierRequest("Текстиль-Опт", "+380441234567"));
        var linen = await CreateAsync(client, "/stock/items", new CreateStockItemRequest(null, supplier, "Льон", "м", MinQuantity: 30, Price: 200));
        (await client.PostAsJsonAsync($"/stock/items/{linen}/adjustments", new AdjustStockRequest(12.5m, "Прихід"))).EnsureSuccessStatusCode();
        await CreateAsync(client, "/stock/items", new CreateStockItemRequest(null, null, "Пакет", "шт", MinQuantity: 10));

        var report = await client.GetFromJsonAsync<ReorderReportDto>("/stock/reorder");

        var textile = report!.Suppliers.Single(s => s.SupplierId == supplier);
        var line = Assert.Single(textile.Lines);
        Assert.Equal((48m, 9600m), (line.Suggested, line.Sum)); // 2 × 30 − 12.5 = 47.5 → 48 м по 200
        Assert.Contains("— Льон: 48 м", textile.MessageText);
        Assert.Equal(ReorderService.NoSupplierName, report.Suppliers[^1].SupplierName);
    }

    [MySqlFact]
    public async Task PurchaseOrder_FromReorder_OrderedReceivedInParts_UpdatesStockAndReport()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);
        var supplier = await CreateAsync(client, "/stock/suppliers", new CreateStockSupplierRequest("Фурнітура-Опт", null));
        var zipper = await CreateAsync(client, "/stock/items", new CreateStockItemRequest(null, supplier, "Блискавка", "шт", MinQuantity: 50, Price: 5));

        // Чернетка з пропозиції звіту: 2 × 50 − 0 = 100 шт по 5.
        var orderId = await CreateAsync(client, $"/stock/purchase-orders/from-reorder/{supplier}", new { });
        var order = await GetOrderAsync(client, orderId);
        var line = Assert.Single(order.Lines);
        Assert.Equal(("draft", 100m, 500m), (order.Status, line.Quantity, order.Total));
        Assert.Contains("— Блискавка: 100 шт", order.MessageText);

        // Звіт більше не пропонує те, що вже в чернетці.
        Assert.DoesNotContain((await GetReportAsync(client)).Suppliers, s => s.SupplierId == supplier);

        (await client.PostAsync($"/stock/purchase-orders/{orderId}/ordered", null)).EnsureSuccessStatusCode();
        await ReceiveAsync(client, orderId, line.Id, 60);
        order = await GetOrderAsync(client, orderId);
        Assert.Equal(("ordered", 40m), (order.Status, order.Lines[0].Remaining));

        await ReceiveAsync(client, orderId, line.Id, 40);
        Assert.Equal("received", (await GetOrderAsync(client, orderId)).Status);

        var item = await client.GetFromJsonAsync<StockItemDto>($"/stock/items/{zipper}");
        Assert.Equal(100, item!.Quantity);
        var movements = await client.GetFromJsonAsync<List<StockMovementDto>>($"/stock/items/{zipper}/movements");
        Assert.All(movements!, m => Assert.Equal($"Прихід за замовленням постачальнику №{orderId}", m.Reason));

        // Отримане замовлення закрите: приймати ще раз не можна.
        var again = await client.PostAsJsonAsync($"/stock/purchase-orders/{orderId}/receive",
            new ReceivePurchaseOrderRequest([new(line.Id, 1)]));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    private static async Task ReceiveAsync(HttpClient client, int orderId, int lineId, decimal quantity) =>
        (await client.PostAsJsonAsync($"/stock/purchase-orders/{orderId}/receive",
            new ReceivePurchaseOrderRequest([new(lineId, quantity)]))).EnsureSuccessStatusCode();

    private static async Task<PurchaseOrderDto> GetOrderAsync(HttpClient client, int id) =>
        (await client.GetFromJsonAsync<PurchaseOrderDto>($"/stock/purchase-orders/{id}"))!;

    private static async Task<ReorderReportDto> GetReportAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<ReorderReportDto>("/stock/reorder"))!;

    private static async Task<int> CreateAsync<T>(HttpClient client, string url, T body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }
}
