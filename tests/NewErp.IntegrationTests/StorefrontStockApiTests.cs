using System.Net.Http.Json;

using NewErp.Application.Orders;
using NewErp.Application.Production;

namespace NewErp.IntegrationTests;

/// <summary>Пошиті залишки на вітрині магазину. Окрема тестова база, бо тест змінює кількість товару в каталозі.</summary>
public class StorefrontStockApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task SewnAndShipped_AreShownOnStorefront()
    {
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        var created = await admin.PostAsJsonAsync("/production/models", new SaveProductionModelRequest("Сорочка лляна", [new("S"), new("M")]));
        var modelId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        var model = (await admin.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}"))!;
        var sizeM = model.Sizes.Single(s => s.Name == "M").Id;

        // Прив'язка через категорію товару CategoryProductId; розмір M — значення опції, вибране в цьому товарі.
        (await admin.PutAsJsonAsync($"/production/models/{modelId}/links", new SetProductionLinksRequest(
            [], [new(ApiFactory.CategoryId, "", false)], [new(sizeM, ApiFactory.SizeMOptionValueId)]))).EnsureSuccessStatusCode();
        Assert.Equal((0, 0), await StorefrontAsync()); // одразу після прив'язки: пошитих ще немає

        (await admin.PostAsJsonAsync($"/production/models/{modelId}/sewn", new ProductionOperationRequest([new(sizeM, 3)]))).EnsureSuccessStatusCode();
        Assert.Equal((3, 3), await StorefrontAsync());

        (await admin.PostAsJsonAsync($"/production/models/{modelId}/ship", new ProductionOperationRequest([new(sizeM, 1)]))).EnsureSuccessStatusCode();
        Assert.Equal((2, 2), await StorefrontAsync());

        var sync = await (await admin.PostAsync("/production/storefront/sync", null)).Content.ReadFromJsonAsync<StorefrontSyncResult>();
        Assert.Equal(new StorefrontSyncResult(1, 1), sync);

        async Task<(int Size, int Product)> StorefrontAsync()
        {
            var product = (await admin.GetFromJsonAsync<List<ProductPickDto>>("/orders/new/products?search=лляна"))!.Single();
            return (product.Options.Single().Quantity, product.Quantity);
        }
    }
}
