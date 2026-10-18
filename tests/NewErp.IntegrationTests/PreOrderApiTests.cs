using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Catalog;
using NewErp.Application.Production;
using NewErp.Domain.Catalog;
using NewErp.Infrastructure.Persistence;

namespace NewErp.IntegrationTests;

/// <summary>Передзамовлення розмірів. Окрема тестова база, бо тест змінює залишок і ліміт розміру в каталозі.</summary>
public class PreOrderApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int SizeM = 502; // product_option_value розміру M товару CategoryProductId

    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Quota_IsEditable_AndSoldAheadIsSettled_AlsoBeforeStorefrontSync()
    {
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        (await manager.PutAsJsonAsync($"/preorders/{SizeM}", new UpdatePreOrderRequest(5, new DateOnly(2026, 11, 20)))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PutAsJsonAsync($"/preorders/{SizeM}", new UpdatePreOrderRequest(-1, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsJsonAsync("/preorders/9999", new UpdatePreOrderRequest(1, null))).StatusCode);

        // Вітрина продала 2 штуки наперед: залишок пішов у мінус.
        await SetQuantityAsync(-2);
        var row = Assert.Single((await manager.GetFromJsonAsync<List<PreOrderRow>>("/preorders?search=лляна&onlyWithQuota=true"))!);
        Assert.Equal((SizeM, "M", -2, 5, new DateOnly(2026, 11, 20), SizeAvailability.PreOrder),
            (row.ProductOptionValueId, row.Size, row.Quantity, row.Quota, row.DateAvailable, row.Availability));

        var settled = await (await manager.PostAsync("/preorders/settle", null)).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<PreOrderSettleResult>();
        Assert.Equal(new PreOrderSettleResult(1), settled);
        Assert.Equal((0, 3), await SizeAsync());

        // Передача пошитих залишків на вітрину спершу списує продане наперед, інакше новий залишок затер би мінус.
        await SetQuantityAsync(-1);
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        var created = await admin.PostAsJsonAsync("/production/models", new SaveProductionModelRequest("Сорочка лляна", [new("M")]));
        var modelId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        var sizeId = (await admin.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}"))!.Sizes.Single().Id;
        (await admin.PutAsJsonAsync($"/production/models/{modelId}/links", new SetProductionLinksRequest(
            [], [new(ApiFactory.CategoryId, "", false)], [new(sizeId, ApiFactory.SizeMOptionValueId)]))).EnsureSuccessStatusCode();
        Assert.Equal((0, 2), await SizeAsync());

        (await manager.PutAsJsonAsync($"/preorders/{SizeM}", new UpdatePreOrderRequest(0, null))).EnsureSuccessStatusCode();
        Assert.Empty((await manager.GetFromJsonAsync<List<PreOrderRow>>("/preorders?onlyWithQuota=true"))!);
        Assert.Null((await manager.GetFromJsonAsync<List<PreOrderRow>>("/preorders?search=лляна"))!.Single().DateAvailable);

        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/preorders")).StatusCode);
    }

    private async Task SetQuantityAsync(int quantity)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ErpDbContext>().DdProductOptionValues
            .Where(v => v.ProductOptionValueId == SizeM).ExecuteUpdateAsync(s => s.SetProperty(v => v.Quantity, quantity).SetProperty(v => v.Subtract, true));
    }

    private async Task<(int Quantity, int Quota)> SizeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ErpDbContext>().DdProductOptionValues.AsNoTracking()
            .Where(v => v.ProductOptionValueId == SizeM).Select(v => new { v.Quantity, v.PreOrderQuantity }).SingleAsync();
        return (row.Quantity, row.PreOrderQuantity);
    }
}
