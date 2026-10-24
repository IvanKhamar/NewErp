using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Locations;
using NewErp.Application.Production;

namespace NewErp.IntegrationTests;

/// <summary>Пошиті вироби на кількох складах: пошито на склад, переміщення, відвантаження зі складу, звіт.</summary>
public class SewnLocationApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task SewnAtWarehouse_Transfer_ShipFromWarehouse_Report()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);
        var warehouse = await CreateAsync(client, "/locations", new SaveLocationRequest("Шоурум"));
        var modelId = await CreateAsync(client, "/production/models", new SaveProductionModelRequest("Сукня для шоуруму", [new("M", 100)]));
        var sizeM = (await client.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}"))!.Sizes.Single().Id;

        await PostOkAsync(client, $"/production/models/{modelId}/sewn", new ProductionOperationRequest([new(sizeM, 6)]));
        await PostOkAsync(client, $"/production/models/{modelId}/sewn", new ProductionOperationRequest([new(sizeM, 2)], WarehouseId: warehouse));
        await PostOkAsync(client, $"/production/models/{modelId}/transfers", new TransferSewnRequest(sizeM, null, warehouse, 3, "На вітрину"));

        var places = (await client.GetFromJsonAsync<List<SizePlacesDto>>($"/production/models/{modelId}/places"))!.Single();
        Assert.Equal(8, places.Sewn);
        Assert.Equal([(null, 3m), (warehouse, 5m)], places.Places.Select(p => (p.WarehouseId, p.Quantity)));

        // З основного не відвантажити того, що лежить у шоурумі; крій на склад не кладуть.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/production/models/{modelId}/ship", new ProductionOperationRequest([new(sizeM, 4)]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/production/models/{modelId}/cut", new ProductionOperationRequest([new(sizeM, 1)], WarehouseId: warehouse))).StatusCode);
        await PostOkAsync(client, $"/production/models/{modelId}/ship", new ProductionOperationRequest([new(sizeM, 4)], WarehouseId: warehouse));

        var report = (await client.GetFromJsonAsync<SewnByWarehouseDto>("/production/by-warehouse"))!;
        var column = report.Places.ToList().FindIndex(p => p.WarehouseId == warehouse);
        var row = report.Rows.Single(r => r.SizeId == sizeM);
        Assert.Equal((4m, 3m, 1m), (row.Total, row.Quantities[0], row.Quantities[column]));

        var transfers = await client.GetFromJsonAsync<List<SewnTransferDto>>($"/production/models/{modelId}/transfers");
        Assert.Equal(("M", "Шоурум"), (transfers![0].SizeName, transfers[0].To));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/locations/{warehouse}")).StatusCode);
    }

    private static async Task PostOkAsync<T>(HttpClient client, string url, T body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<int> CreateAsync<T>(HttpClient client, string url, T body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }
}
