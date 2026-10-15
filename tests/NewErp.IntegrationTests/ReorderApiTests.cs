using System.Net.Http.Json;

using NewErp.Application.Stock;

namespace NewErp.IntegrationTests;

public class ReorderApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Report_GroupsItemsBelowMinimumBySupplierWithSuggestedQuantity()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);
        var supplier = await CreateAsync(client, "/stock/suppliers", new CreateStockSupplierRequest("Текстиль-Опт", "+380441234567"));
        var linen = await CreateAsync(client, "/stock/items", new CreateStockItemRequest(null, supplier, "Льон", "м", MinQuantity: 30, Price: 200));
        (await client.PostAsJsonAsync($"/stock/items/{linen}/adjustments", new AdjustStockRequest(12.5m, "Прихід"))).EnsureSuccessStatusCode();
        await CreateAsync(client, "/stock/items", new CreateStockItemRequest(null, null, "Пакет", "шт", MinQuantity: 10));

        var report = await client.GetFromJsonAsync<ReorderReportDto>("/stock/reorder");

        var textile = report!.Suppliers.Single(s => s.SupplierId == supplier);
        var line = Assert.Single(textile.Lines);
        Assert.Equal((48m, 9600m), (line.Suggested, line.Sum)); // 2 × 30 − 12.5 = 47.5 → 48 м по 200
        Assert.Contains("— Льон: 48 м", textile.MessageText);
        Assert.Equal(ReorderService.NoSupplierName, report.Suppliers[^1].SupplierName);
    }

    private static async Task<int> CreateAsync<T>(HttpClient client, string url, T body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }
}
