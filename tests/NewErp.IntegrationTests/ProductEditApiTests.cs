using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Catalog;
using NewErp.Application.Common;

namespace NewErp.IntegrationTests;

/// <summary>
/// Зміна товару, перенесено з Java ProductControllerTest. Окремий клас — окрема база: тут товари змінюються, а
/// ProductApiTests перевіряє список на тих самих вихідних даних.
/// </summary>
public class ProductEditApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateTime DressVersion = new(2026, 10, 1, 10, 0, 0);
    private static readonly DateTime OtherVersion = new(2026, 1, 1);

    [MySqlFact]
    public async Task Patch_ChangesOnlyTheVersionSeen_AndRefusesBadValues()
    {
        await ProductApiTests.SeedAsync(factory);
        var manager = await factory.LoginAsync(ApiFactory.Manager);

        var changed = await manager.PatchAsJsonAsync("/catalog/products/70", new UpdateProductRequest(1399.50m, false, [2], [30, 31], DressVersion));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var card = (await changed.Content.ReadFromJsonAsync<ProductCardDto>())!;
        Assert.Equal((1399.50m, false), (card.Price, card.Enabled));
        Assert.Equal([2], card.Labels.Select(l => l.Id));
        Assert.Equal([30, 31], card.Categories.Select(c => c.Id).Order());
        Assert.NotEqual(DressVersion, card.DateModified);
        Assert.Equal(2, card.Sizes.Count); // розміри й залишки не чіпаємо

        // Та сама стара версія ще раз: товар відтоді змінили (тут — ми самі).
        Assert.Equal(HttpStatusCode.Conflict,
            (await manager.PatchAsJsonAsync("/catalog/products/70", new UpdateProductRequest(1000m, DateModified: DressVersion))).StatusCode);
        Assert.Equal(1399.50m, (await manager.GetFromJsonAsync<ProductCardDto>("/catalog/products/70"))!.Price);

        // Null — без змін: у 72 міняються лише мітки.
        var labelsOnly = (await (await manager.PatchAsJsonAsync("/catalog/products/72", new UpdateProductRequest(LabelIds: [1], DateModified: OtherVersion)))
            .Content.ReadFromJsonAsync<ProductCardDto>())!;
        Assert.Equal((900m, true), (labelsOnly.Price, labelsOnly.Enabled));
        Assert.Equal([new LookupItem(1, "Хіт")], labelsOnly.Labels);
        Assert.Equal([30], labelsOnly.Categories.Select(c => c.Id));

        async Task<HttpStatusCode> Patch71(UpdateProductRequest request) =>
            (await manager.PatchAsJsonAsync("/catalog/products/71", request)).StatusCode;
        Assert.Equal(HttpStatusCode.BadRequest, await Patch71(new UpdateProductRequest(-1m, DateModified: OtherVersion)));
        Assert.Equal(HttpStatusCode.BadRequest, await Patch71(new UpdateProductRequest(10.005m, DateModified: OtherVersion)));
        Assert.Equal(HttpStatusCode.BadRequest, await Patch71(new UpdateProductRequest(LabelIds: [9], DateModified: OtherVersion)));
        Assert.Equal(HttpStatusCode.BadRequest, await Patch71(new UpdateProductRequest(CategoryIds: [], DateModified: OtherVersion)));
        // Категорія 32 вимкнена: обрати її наново не можна.
        Assert.Equal(HttpStatusCode.BadRequest, await Patch71(new UpdateProductRequest(CategoryIds: [32], DateModified: OtherVersion)));
        Assert.Equal(HttpStatusCode.BadRequest, await Patch71(new UpdateProductRequest(100m)));
        Assert.Equal(HttpStatusCode.NotFound,
            (await manager.PatchAsJsonAsync("/catalog/products/999", new UpdateProductRequest(100m, DateModified: OtherVersion))).StatusCode);
        // Відхилені зміни товару не торкнулися.
        var untouched = (await manager.GetFromJsonAsync<ProductCardDto>("/catalog/products/71"))!;
        Assert.Equal((500m, OtherVersion), (untouched.Price, untouched.DateModified));
        Assert.Equal([31], untouched.Categories.Select(c => c.Id));

        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await storekeeper.PatchAsJsonAsync("/catalog/products/70", new UpdateProductRequest(1m, DateModified: DressVersion))).StatusCode);
    }
}
