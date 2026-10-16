using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Staff;
using NewErp.Application.Stock;

namespace NewErp.IntegrationTests;

public class StockAndBonusApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Stock_CreateAdjustAndReadMovements()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var created = await client.PostAsJsonAsync("/stock/items", new SaveStockItemRequest(1, null, "Льон", "м", MinQuantity: 10));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<Created>())!.Id;

        var lowBefore = await client.GetFromJsonAsync<List<StockItemDto>>("/stock/items?belowMinimumOnly=true");
        Assert.Contains(lowBefore!, i => i.Id == id);

        var income = await client.PostAsJsonAsync($"/stock/items/{id}/adjustments", new AdjustStockRequest(15, "Прихід"));
        income.EnsureSuccessStatusCode();
        var tooMuch = await client.PostAsJsonAsync($"/stock/items/{id}/adjustments", new AdjustStockRequest(-20, "Пошиття"));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);

        var item = await client.GetFromJsonAsync<StockItemDto>($"/stock/items/{id}");
        Assert.Equal(15, item!.Quantity);
        Assert.False(item.IsBelowMinimum);
        var movements = await client.GetFromJsonAsync<List<StockMovementDto>>($"/stock/items/{id}/movements");
        Assert.Single(movements!);
    }

    [MySqlFact]
    public async Task UpdateItem_ChangesSupplierAndName_KeepsQuantity()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);
        var supplier = (await (await client.PostAsJsonAsync("/stock/suppliers", new CreateStockSupplierRequest("Нитки-Опт", null)))
            .Content.ReadFromJsonAsync<Created>())!.Id;
        var created = await client.PostAsJsonAsync("/stock/items", new SaveStockItemRequest(1, null, "Нитки", "шт", MinQuantity: 5));
        var id = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        (await client.PostAsJsonAsync($"/stock/items/{id}/adjustments", new AdjustStockRequest(7, "Прихід"))).EnsureSuccessStatusCode();

        var update = await client.PutAsJsonAsync($"/stock/items/{id}", new SaveStockItemRequest(1, supplier, "Нитки чорні", "шт", 10, Price: 25));
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var item = await client.GetFromJsonAsync<StockItemDto>($"/stock/items/{id}");
        Assert.Equal(("Нитки чорні", "Нитки-Опт", 10m, 25m, 7m), (item!.Name, item.SupplierName, item.MinQuantity, item.Price, item.Quantity));

        var unknownSupplier = await client.PutAsJsonAsync($"/stock/items/{id}", new SaveStockItemRequest(1, 9999, "Нитки", "шт", 0));
        Assert.Equal(HttpStatusCode.BadRequest, unknownSupplier.StatusCode);
    }

    [MySqlFact]
    public async Task MalformedJson_IsBadRequestNotServerError()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var response = await client.PostAsync("/stock/items", new StringContent("{ \"name\": ", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("JSON", await response.Content.ReadAsStringAsync());
    }

    [MySqlFact]
    public async Task Bonuses_CountCompletedOrdersOfManager()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var report = await client.GetFromJsonAsync<BonusReport>("/staff/bonuses?from=2026-10-01&to=2026-10-31");

        var row = Assert.Single(report!.Managers);
        Assert.Equal(200m, row.Bonus); // 10% від замовлення №2 на 2000
    }
}
