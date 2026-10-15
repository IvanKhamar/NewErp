using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Production;
using NewErp.Application.Stock;

namespace NewErp.IntegrationTests;

public class ProductionApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Model_SizesComponentsBomsAndLinks_RoundTrip()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);
        var linen = await CreateIdAsync(client, "/stock/items", new CreateStockItemRequest(null, null, "Льон", "м", 0));
        var thread = await CreateIdAsync(client, "/stock/items", new CreateStockItemRequest(null, null, "Нитки", "шт", 0));

        var modelId = await CreateIdAsync(client, "/production/models",
            new SaveProductionModelRequest("Сукня лляна", [new("S", 30), new("M", 40), new("L", 30)], MinTotal: 20));
        var front = await CreateIdAsync(client, $"/production/models/{modelId}/components", new SaveComponentRequest("Перед"));
        var model = await GetModelAsync(client, modelId);
        var sizeM = model.Sizes.Single(s => s.Name == "M").Id;

        await PutOkAsync(client, $"/production/components/{front}/bom",
            new SetComponentBomRequest([new(null, linen, 1.2m), new(sizeM, linen, 1.4m)]));
        await PutOkAsync(client, $"/production/models/{modelId}/sewn-bom",
            new SetSewnBomRequest([new(null, front, null, 1), new(null, null, thread, 0.5m)]));
        await PutOkAsync(client, $"/production/models/{modelId}/links",
            new SetProductionLinksRequest([ApiFactory.ProductId], [new(5, "сукня")], [new(sizeM, 46)]));

        // Перейменували розміри: M лишився тим самим (id і посилання в специфікації не зламалися), S прибрано, додано XL.
        await PutOkAsync(client, $"/production/models/{modelId}",
            new SaveProductionModelRequest("Сукня лляна", [new("M", 50), new("L", 30), new("XL", 20)], MinTotal: 20));

        model = await GetModelAsync(client, modelId);
        Assert.Equal(["M", "L", "XL"], model.Sizes.Select(s => s.Name));
        Assert.Equal(sizeM, model.Sizes[0].Id);
        Assert.Equal(46, model.Sizes[0].OptionValueId);
        Assert.Equal([ApiFactory.ProductId], model.ProductIds);
        Assert.Equal("сукня", Assert.Single(model.Categories).NameFilter);
        var component = Assert.Single(model.Components);
        Assert.Equal(1.4m, component.Bom.Single(b => b.SizeId == sizeM).QtyPerUnit);
        Assert.Equal("Льон", component.Bom[0].ItemName);
        Assert.Equal(2, model.SewnBom.Count);
        Assert.Contains(model.SewnBom, b => b.ComponentName == "Перед");

        var belowMinimum = await client.GetFromJsonAsync<List<ProductionModelSummaryDto>>("/production/models?belowMinimumOnly=true");
        Assert.Equal(20, belowMinimum!.Single(m => m.Id == modelId).Deficit); // пошитих ще 0 із 20

        // Видалення деталі прибирає її й зі специфікації пошиву.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/production/components/{front}")).StatusCode);
        model = await GetModelAsync(client, modelId);
        Assert.Empty(model.Components);
        Assert.Equal(thread, Assert.Single(model.SewnBom).ItemId);
    }

    [MySqlFact]
    public async Task SewnBom_UnknownItem_BadRequest()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);
        var modelId = await CreateIdAsync(client, "/production/models", new SaveProductionModelRequest("Футболка", [new("S")]));

        var response = await client.PutAsJsonAsync($"/production/models/{modelId}/sewn-bom",
            new SetSewnBomRequest([new(null, null, 99999, 1)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [MySqlFact]
    public async Task Production_ManagerHasNoAccess()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/production/models")).StatusCode);
    }

    private static async Task<ProductionModelDto> GetModelAsync(HttpClient client, int id) =>
        (await client.GetFromJsonAsync<ProductionModelDto>($"/production/models/{id}"))!;

    private static async Task<int> CreateIdAsync<T>(HttpClient client, string url, T body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private static async Task PutOkAsync<T>(HttpClient client, string url, T body)
    {
        var response = await client.PutAsJsonAsync(url, body);
        Assert.True(response.IsSuccessStatusCode, $"{url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }
}
