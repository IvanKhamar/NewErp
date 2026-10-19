using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Orders;
using NewErp.Application.Production;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>План пошиву. Окрема тестова база, бо тест змінює статус замовлення OrderId і пошиті залишки.</summary>
public class SewingPlanApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int DressCategoryId = 7;

    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Plan_CountsWhatActiveOrdersMiss_MinusFreeSewn_AndWhatOrdersAlreadyHold()
    {
        await AddCategoryAsync();
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        var created = await storekeeper.PostAsJsonAsync("/production/models", new SaveProductionModelRequest("Сукня", [new("S"), new("M")]));
        var modelId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        var sizeM = (await storekeeper.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}"))!.Sizes.Single(s => s.Name == "M").Id;
        (await storekeeper.PutAsJsonAsync($"/production/models/{modelId}/links",
            new SetProductionLinksRequest([ApiFactory.ProductId], [], [new(sizeM, ApiFactory.SizeMOptionValueId)]))).EnsureSuccessStatusCode();
        (await storekeeper.PostAsJsonAsync($"/production/models/{modelId}/cut",
            new ProductionOperationRequest([new(sizeM, 1)]))).EnsureSuccessStatusCode();

        // Замовлення OrderId («Уточнюємо») хоче 2 M, пошитих немає, 1 розкроєно: пошити 2, з них 1 з крою, розкроїти ще 1.
        var plan = await PlanAsync(storekeeper);
        var row = Assert.Single(plan.Rows);
        Assert.Equal((modelId, "M", 2m, 2m, 2m, 1m, 1m),
            (row.ProductionId, row.SizeName, row.Ordered, row.Missing, row.ToSew, row.FromCut, row.ToCut));
        Assert.Equal([ApiFactory.OrderId], row.OrderIds);
        // Дата замовлення 01.10.2026 + 10 днів терміну категорії.
        Assert.Equal(new DateOnly(2026, 10, 11), row.Deadline);
        // Сорочка іншого замовлення не прив'язана до моделі: її видно в «не враховано».
        Assert.Contains(plan.Issues, i => i.OrderIds.Contains(ApiFactory.CategoryOrderId) && i.Reason.Contains("Не прив'язано"));

        // Пошили 1 і прийняли замовлення: воно забрало 1 зі складу, бракує ще 1.
        (await storekeeper.PostAsJsonAsync($"/production/models/{modelId}/sewn",
            new ProductionOperationRequest([new(sizeM, 1)]))).EnsureSuccessStatusCode();
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        (await admin.PutAsJsonAsync($"/orders/{ApiFactory.OrderId}/status", new ChangeOrderStatusRequest(ApiFactory.Accepted))).EnsureSuccessStatusCode();
        row = Assert.Single((await PlanAsync(storekeeper)).Rows);
        Assert.Equal((2m, 1m, 1m, 0m), (row.Ordered, row.Missing, row.ToSew, row.SewnStock));

        // Дошили ще 1: вільний запас покриває решту, і в плані «лише що шити» рядка немає.
        (await storekeeper.PostAsJsonAsync($"/production/models/{modelId}/sewn",
            new ProductionOperationRequest([new(sizeM, 1)]))).EnsureSuccessStatusCode();
        Assert.Empty((await PlanAsync(storekeeper)).Rows);
        Assert.Equal(0m, Assert.Single((await storekeeper.GetFromJsonAsync<SewingPlanDto>("/production/plan?onlyToSew=false"))!.Rows).ToSew);

        var manager = await factory.LoginAsync(ApiFactory.Manager);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/production/plan")).StatusCode);
    }

    private static async Task<SewingPlanDto> PlanAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<SewingPlanDto>("/production/plan"))!;

    private async Task AddCategoryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.DdCategories.Add(new DdCategory { CategoryId = DressCategoryId, ProductionPeriod = 10, Status = true });
        db.DdProductToCategories.Add(new DdProductToCategory { ProductId = ApiFactory.ProductId, CategoryId = DressCategoryId });
        await db.SaveChangesAsync();
    }
}
