using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Stock;
using NewErp.Infrastructure.Persistence;

namespace NewErp.IntegrationTests;

/// <summary>
/// Пункт 72: реконструйовані таблиці звірено зі скриптами старого сайту (Vidro_DB). Розхідники тримають два знаки після
/// коми, а позиції мають зовнішні ключі на категорії й постачальників з ON DELETE SET NULL.
/// Тести перенесено з tests/test_reconstructed_schema.py версії на Python.
/// </summary>
public class ReconstructedSchemaApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    private async Task<int> CreateItemAsync(HttpClient client, int? categoryId, string name)
    {
        var created = await client.PostAsJsonAsync("/stock/items", new SaveStockItemRequest(categoryId, null, name, "м", 0));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private async Task<T> WithDbAsync<T>(Func<ErpDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    [MySqlFact]
    public async Task ThreeDecimals_AreRefused_AndTwoAreStored()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);
        var id = await CreateItemAsync(client, 1, "Стрічка");

        var refused = await client.PostAsJsonAsync($"/stock/items/{id}/adjustments", new AdjustStockRequest(1.255m, "Прихід"));
        var stored = await client.PostAsJsonAsync($"/stock/items/{id}/adjustments", new AdjustStockRequest(1.25m, "Прихід"));

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.OK), (refused.StatusCode, stored.StatusCode));
        Assert.Contains("Зміна залишку: не більше 2 знаків після коми.", await refused.Content.ReadAsStringAsync());
        var item = await client.GetFromJsonAsync<StockItemDto>($"/stock/items/{id}");
        Assert.Equal(1.25m, item!.Quantity);
    }

    [MySqlFact]
    public async Task DeletingACategory_LeavesItsItemsWithoutOne()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);
        var category = await client.PostAsJsonAsync("/stock/categories", new CreateStockCategoryRequest("Тимчасова"));
        var categoryId = (await category.Content.ReadFromJsonAsync<Created>())!.Id;
        var itemId = await CreateItemAsync(client, categoryId, "Скоби");

        // The real table has foreign keys with ON DELETE SET NULL, as the old module's deleteCategory expects.
        var left = await WithDbAsync(async db =>
        {
            await db.StockCategories.Where(c => c.Id == categoryId).ExecuteDeleteAsync();
            return await db.StockItems.AsNoTracking().Where(i => i.Id == itemId).Select(i => i.CategoryId).SingleAsync();
        });

        Assert.Null(left);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/stock/items/{itemId}")).StatusCode);
    }
}
