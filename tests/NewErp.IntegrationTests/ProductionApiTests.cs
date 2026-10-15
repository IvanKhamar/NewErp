using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Orders;
using NewErp.Application.Production;
using NewErp.Application.Stock;

namespace NewErp.IntegrationTests;

public class ProductionApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Bom_SetReplaceAndCalculateOrderRequirements()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);
        var linen = await CreateItemAsync(client, "Льон", "м", income: 3);
        var buttons = await CreateItemAsync(client, "Ґудзики", "шт", income: 50);

        var set = await client.PutAsJsonAsync($"/production/boms/{ApiFactory.ProductId}",
            new SetBomRequest([new(linen, 2.5m), new(buttons, 6)]));
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        // Друга відправка замінює специфікацію: ґудзиків стає 8, а не додається ще один рядок.
        (await client.PutAsJsonAsync($"/production/boms/{ApiFactory.ProductId}",
            new SetBomRequest([new(linen, 2.5m), new(buttons, 8)]))).EnsureSuccessStatusCode();

        var bom = await client.GetFromJsonAsync<BomDto>($"/production/boms/{ApiFactory.ProductId}");
        Assert.Equal(8, bom!.Lines.Single(l => l.StockItemId == buttons).Quantity);

        var requirements = await client.GetFromJsonAsync<OrderRequirementsDto>($"/production/orders/{ApiFactory.OrderId}/requirements");
        var linenNeed = requirements!.Materials.Single(m => m.StockItemId == linen);
        Assert.Equal(5, linenNeed.Required); // 2 сукні × 2.5 м
        Assert.Equal(2, linenNeed.Shortage); // на складі 3 м
        Assert.Empty(requirements.ProductsWithoutBom);
    }

    [MySqlFact]
    public async Task StatusInSewing_WritesOffOnceAndWarnsAboutShortage()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);
        var linen = await CreateItemAsync(client, "Льон для списання", "м", income: 4);
        var tags = await CreateItemAsync(client, "Бірки", "шт", income: 10);
        (await client.PutAsJsonAsync($"/production/boms/{ApiFactory.ProductId}",
            new SetBomRequest([new(linen, 2.5m), new(tags, 1)]))).EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync($"/orders/{ApiFactory.OrderId}/status", new ChangeOrderStatusRequest(ApiFactory.InSewing));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChangeOrderStatusResult>();
        Assert.Contains("Бракує 1 м", Assert.Single(result!.Warnings)); // 2 сукні × 2.5 м = 5 м, є 4 м

        // Повернули статус назад і знову поставили «В пошиві»: другого списання немає.
        (await client.PutAsJsonAsync($"/orders/{ApiFactory.OrderId}/status", new ChangeOrderStatusRequest(ApiFactory.Pending))).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/orders/{ApiFactory.OrderId}/status", new ChangeOrderStatusRequest(ApiFactory.InSewing))).EnsureSuccessStatusCode();

        Assert.Equal(0, (await client.GetFromJsonAsync<StockItemDto>($"/stock/items/{linen}"))!.Quantity);
        Assert.Equal(8, (await client.GetFromJsonAsync<StockItemDto>($"/stock/items/{tags}"))!.Quantity);
        var writeOff = await client.GetFromJsonAsync<OrderWriteOffDto>($"/production/orders/{ApiFactory.OrderId}/writeoff");
        Assert.Equal(1, writeOff!.Lines.Single(l => l.StockItemId == linen).Shortage);
    }

    [MySqlFact]
    public async Task Bom_UnknownStockItem_BadRequest()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var response = await client.PutAsJsonAsync("/production/boms/500", new SetBomRequest([new(9999, 1)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [MySqlFact]
    public async Task Bom_ManagerHasNoAccess()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var response = await client.GetAsync("/production/boms");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<int> CreateItemAsync(HttpClient client, string name, string unit, decimal income)
    {
        var created = await client.PostAsJsonAsync("/stock/items", new CreateStockItemRequest(1, null, name, unit, MinQuantity: 0));
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        (await client.PostAsJsonAsync($"/stock/items/{id}/adjustments", new AdjustStockRequest(income, "Прихід"))).EnsureSuccessStatusCode();
        return id;
    }
}
