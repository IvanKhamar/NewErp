using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Locations;
using NewErp.Application.Stock;

namespace NewErp.IntegrationTests;

/// <summary>Розхідники на кількох складах: переміщення, коригування на складі, звіт, приймання поставки на склад.</summary>
public class StockLocationApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Transfer_Adjust_Report_AndWarehouseWithStockCannotBeDeleted()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);
        var warehouse = await CreateAsync(client, "/locations", new SaveLocationRequest("Склад на Подолі"));
        var item = await CreateAsync(client, "/stock/items", new SaveStockItemRequest(1, null, "Льон для складів", "м", 0, Price: 100));
        (await client.PostAsJsonAsync($"/stock/items/{item}/adjustments", new AdjustStockRequest(30, "Прихід"))).EnsureSuccessStatusCode();

        var moved = await client.PostAsJsonAsync($"/stock/items/{item}/transfers", new TransferStockRequest(null, warehouse, 10, "На Поділ"));
        moved.EnsureSuccessStatusCode();
        var places = (await moved.Content.ReadFromJsonAsync<ItemPlacementDto>())!;
        Assert.Equal(30, places.Total);
        Assert.Equal([(null, 20m), (warehouse, 10m)], places.Places.Select(p => (p.WarehouseId, p.Quantity)));

        // Основний склад не витрачає того, що лежить на Подолі, а на Подолі — не більше, ніж там є.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/stock/items/{item}/adjustments", new AdjustStockRequest(-25, "Пошиття"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/stock/items/{item}/warehouses/{warehouse}/adjustments", new AdjustStockRequest(-11, "Брак"))).StatusCode);
        var used = await client.PostAsJsonAsync($"/stock/items/{item}/warehouses/{warehouse}/adjustments", new AdjustStockRequest(-4, "Брак"));
        used.EnsureSuccessStatusCode();
        Assert.Contains("Склад на Подолі", (await used.Content.ReadFromJsonAsync<StockMovementDto>())!.Reason);

        var report = (await client.GetFromJsonAsync<StockByWarehouseDto>("/stock/by-warehouse"))!;
        var column = report.Places.ToList().FindIndex(p => p.WarehouseId == warehouse);
        var row = report.Rows.Single(r => r.ItemId == item);
        Assert.Equal((20m, 6m), (row.Quantities[0], row.Quantities[column]));
        Assert.Equal(600m, report.Places[column].Value);

        var transfers = await client.GetFromJsonAsync<List<StockTransferDto>>($"/stock/items/{item}/transfers");
        Assert.Equal(("Основний склад", "Склад на Подолі"), (transfers![0].From, transfers[0].To));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/locations/{warehouse}")).StatusCode);
    }

    [MySqlFact]
    public async Task ReceivePurchaseOrder_ToWarehouse_PutsDeliveryThere()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);
        var warehouse = await CreateAsync(client, "/locations", new SaveLocationRequest("Склад у Львові"));
        var supplier = await CreateAsync(client, "/stock/suppliers", new CreateStockSupplierRequest("Фурнітура-Львів", null));
        var item = await CreateAsync(client, "/stock/items", new SaveStockItemRequest(1, supplier, "Ґудзики", "шт", 0));
        var order = await CreateAsync(client, "/stock/purchase-orders", new CreatePurchaseOrderRequest(supplier, [new(item, 50)]));
        (await client.PostAsync($"/stock/purchase-orders/{order}/ordered", null)).EnsureSuccessStatusCode();
        var line = (await client.GetFromJsonAsync<PurchaseOrderDto>($"/stock/purchase-orders/{order}"))!.Lines.Single();

        var received = await client.PostAsJsonAsync($"/stock/purchase-orders/{order}/receive", new ReceivePurchaseOrderRequest([new(line.Id, 50)], warehouse));

        Assert.Equal(HttpStatusCode.NoContent, received.StatusCode);
        var places = (await client.GetFromJsonAsync<ItemPlacementDto>($"/stock/items/{item}/places"))!;
        Assert.Equal([(null, 0m), (warehouse, 50m)], places.Places.Select(p => (p.WarehouseId, p.Quantity)));
    }

    private static async Task<int> CreateAsync<T>(HttpClient client, string url, T body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }
}
