using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Orders;
using NewErp.Application.Production;

namespace NewErp.IntegrationTests;

/// <summary>Пошиті вироби й статуси замовлення. Окрема тестова база, бо тест змінює статус замовлення OrderId.</summary>
public class OrderSewnStockApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task OrderStatuses_TakeSewnBySize_KeepInsideSet_ReturnOnlyWhatWasTaken()
    {
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        var admin = await factory.LoginAsync(ApiFactory.Admin);

        var created = await storekeeper.PostAsJsonAsync("/production/models",
            new SaveProductionModelRequest("Сукня лляна", [new("S"), new("M")]));
        var modelId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        var sizeM = (await GetModelAsync(storekeeper)).Sizes.Single(s => s.Name == "M").Id;
        (await storekeeper.PutAsJsonAsync($"/production/models/{modelId}/links",
            new SetProductionLinksRequest([ApiFactory.ProductId], [], [new(sizeM, ApiFactory.SizeMOptionValueId)]))).EnsureSuccessStatusCode();
        (await storekeeper.PostAsJsonAsync($"/production/models/{modelId}/sewn",
            new ProductionOperationRequest([new(sizeM, 1)]))).EnsureSuccessStatusCode(); // пошито лише 1, а замовлено 2

        // «Уточнюємо» → «Прийнято»: вхід у статуси списання. Є лише 1 з 2 — списується 1 і є попередження.
        var accepted = await ChangeStatusAsync(admin, ApiFactory.Accepted);
        Assert.Contains("бракує 1 шт.", Assert.Single(accepted.Warnings));
        Assert.Equal(0, await SewnMAsync());

        // Тим часом дошили 3. Перехід усередині набору («Прийнято» → «Виготовляється») нічого не змінює.
        (await storekeeper.PostAsJsonAsync($"/production/models/{modelId}/sewn",
            new ProductionOperationRequest([new(sizeM, 3)]))).EnsureSuccessStatusCode();
        Assert.Empty((await ChangeStatusAsync(admin, ApiFactory.Manufacturing)).Warnings);
        Assert.Equal(3, await SewnMAsync());

        // «Відмінено»: вихід із набору повертає лише те, що замовлення справді забрало (1), а не всі 2.
        await ChangeStatusAsync(admin, ApiFactory.Cancelled);
        Assert.Equal(4, await SewnMAsync());

        // Журнал має ті самі коментарі, що й старий сайт.
        var log = await storekeeper.GetFromJsonAsync<List<ProductionLogDto>>($"/production/log?productionId={modelId}");
        Assert.Contains(log!, l => l.Comment == $"Замовлення #{ApiFactory.OrderId}" && l.Direction == "out" && l.Username == "system");
        Assert.Contains(log!, l => l.Comment == $"Скасування #{ApiFactory.OrderId}" && l.Direction == "in" && l.Quantity == 1);

        async Task<decimal> SewnMAsync() => (await GetModelAsync(storekeeper)).Sizes.Single(s => s.Id == sizeM).SewnStock;

        async Task<ProductionModelDto> GetModelAsync(HttpClient client) =>
            (await client.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}"))!;
    }

    [MySqlFact]
    public async Task ProductLinkedThroughCategoryNameFilter_IsTakenToo()
    {
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        var admin = await factory.LoginAsync(ApiFactory.Admin);

        var created = await storekeeper.PostAsJsonAsync("/production/models", new SaveProductionModelRequest("Сорочка", [new("M")]));
        var modelId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        var sizeM = (await storekeeper.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}"))!.Sizes[0].Id;
        // Не напряму, а «усі товари категорії, назва яких містить "ОВЕРСАЙЗ"» (регістр не важливий, як у старому модулі).
        (await storekeeper.PutAsJsonAsync($"/production/models/{modelId}/links",
            new SetProductionLinksRequest([], [new(ApiFactory.CategoryId, "ОВЕРСАЙЗ")], [new(sizeM, ApiFactory.SizeMOptionValueId)]))).EnsureSuccessStatusCode();
        (await storekeeper.PostAsJsonAsync($"/production/models/{modelId}/sewn",
            new ProductionOperationRequest([new(sizeM, 5)]))).EnsureSuccessStatusCode();

        Assert.Empty((await ChangeStatusAsync(admin, ApiFactory.Accepted, ApiFactory.CategoryOrderId)).Warnings);

        var model = await storekeeper.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}");
        Assert.Equal(4, model!.Sizes[0].SewnStock);
    }

    private static async Task<ChangeOrderStatusResult> ChangeStatusAsync(HttpClient client, int statusId, int orderId = ApiFactory.OrderId)
    {
        var response = await client.PutAsJsonAsync($"/orders/{orderId}/status", new ChangeOrderStatusRequest(statusId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChangeOrderStatusResult>())!;
    }
}
