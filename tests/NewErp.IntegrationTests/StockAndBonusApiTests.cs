using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Staff;
using NewErp.Application.Stock;

namespace NewErp.IntegrationTests;

public class StockAndBonusApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Stock_CreateAdjustAndReadMovements()
    {
        var created = await _client.PostAsJsonAsync("/stock/items", new CreateStockItemRequest(1, null, "Льон", "м", MinQuantity: 10));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<Created>())!.Id;

        var lowBefore = await _client.GetFromJsonAsync<List<StockItemDto>>("/stock/items?belowMinimumOnly=true");
        Assert.Contains(lowBefore!, i => i.Id == id);

        var income = await _client.PostAsJsonAsync($"/stock/items/{id}/adjustments", new AdjustStockRequest(15, "Прихід", 1));
        income.EnsureSuccessStatusCode();
        var tooMuch = await _client.PostAsJsonAsync($"/stock/items/{id}/adjustments", new AdjustStockRequest(-20, "Пошиття", 1));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);

        var item = await _client.GetFromJsonAsync<StockItemDto>($"/stock/items/{id}");
        Assert.Equal(15, item!.Quantity);
        Assert.False(item.IsBelowMinimum);
        var movements = await _client.GetFromJsonAsync<List<StockMovementDto>>($"/stock/items/{id}/movements");
        Assert.Single(movements!);
    }

    [MySqlFact]
    public async Task Bonuses_CountCompletedOrdersOfManager()
    {
        var report = await _client.GetFromJsonAsync<BonusReport>("/staff/bonuses?from=2026-10-01&to=2026-10-31");

        var row = Assert.Single(report!.Managers);
        Assert.Equal(200m, row.Bonus); // 10% від замовлення №2 на 2000
    }
}