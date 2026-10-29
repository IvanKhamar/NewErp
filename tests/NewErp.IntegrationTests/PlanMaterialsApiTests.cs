using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Production;
using NewErp.Application.Stock;

namespace NewErp.IntegrationTests;

/// <summary>Розхідники для плану пошиву. Окрема тестова база, бо тест створює модель, розхідники й специфікації.</summary>
public class PlanMaterialsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Materials_ForWhatThePlanMustCutAndSew_WithStockAndShortage()
    {
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        var fabric = await CreateAsync(storekeeper, "/stock/items", new SaveStockItemRequest(null, null, "Тканина льон", "м", 0));
        var thread = await CreateAsync(storekeeper, "/stock/items", new SaveStockItemRequest(null, null, "Нитки білі", "шт", 0));
        (await storekeeper.PostAsJsonAsync($"/stock/items/{fabric}/adjustments", new AdjustStockRequest(1, "Прихід"))).EnsureSuccessStatusCode();

        var modelId = await CreateAsync(storekeeper, "/production/models", new SaveProductionModelRequest("Сукня", [new("S"), new("M")]));
        var sizeM = (await storekeeper.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}"))!.Sizes.Single(s => s.Name == "M").Id;
        (await storekeeper.PutAsJsonAsync($"/production/models/{modelId}/links",
            new SetProductionLinksRequest([ApiFactory.ProductId], [], [new(sizeM, ApiFactory.SizeMOptionValueId)]))).EnsureSuccessStatusCode();
        var front = await CreateAsync(storekeeper, $"/production/models/{modelId}/components", new SaveComponentRequest("Перед"));
        (await storekeeper.PutAsJsonAsync($"/production/components/{front}/bom",
            new SetComponentBomRequest([new(null, fabric, 0.8m)]))).EnsureSuccessStatusCode();
        (await storekeeper.PutAsJsonAsync($"/production/models/{modelId}/sewn-bom",
            new SetSewnBomRequest([new(null, front, null, 1), new(null, null, thread, 0.1m)]))).EnsureSuccessStatusCode();

        // Замовлення OrderId хоче 2 M, нічого не пошито й не розкроєно: розкроїти 2 (тканина 1.6 м), пошити 2 (нитки 0.2).
        var report = (await storekeeper.GetFromJsonAsync<PlanMaterialsDto>("/production/plan/materials"))!;

        Assert.Empty(report.Warnings);
        Assert.Equal(["Нитки білі", "Тканина льон"], report.Materials.Select(m => m.Name)); // обидва бракують, тож за назвою
        var threads = report.Materials[0];
        Assert.Equal((0.2m, 0m, 0m, 0.2m), (threads.Needed, threads.InStock, threads.OnOrder, threads.Shortage));
        var fabrics = report.Materials[1];
        Assert.Equal((1.6m, 1m, 0.6m), (fabrics.Needed, fabrics.InStock, fabrics.Shortage));
        var use = Assert.Single(fabrics.Uses);
        Assert.Equal((modelId, "Сукня", "M", "Крій", 1.6m), (use.ProductionId, use.ModelName, use.SizeName, use.Stage, use.Quantity));

        var manager = await factory.LoginAsync(ApiFactory.Manager);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/production/plan/materials")).StatusCode);
    }

    private static async Task<int> CreateAsync<T>(HttpClient client, string url, T request)
    {
        var response = await client.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }
}
