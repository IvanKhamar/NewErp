using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using ClosedXML.Excel;

using NewErp.Application.Production;
using NewErp.Application.Stock;

namespace NewErp.IntegrationTests;

/// <summary>Інвентаризація: аркуш → заповнили → перегляд → запис; інвентаризація моделі.</summary>
public class StocktakingApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Sheet_Preview_Apply_WritesDifferencesToLog()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);
        var item = await CreateAsync(client, "/stock/items", new SaveStockItemRequest(null, null, "Льон для інвентаризації", "м", 0, Price: 50));
        (await client.PostAsJsonAsync($"/stock/items/{item}/adjustments", new AdjustStockRequest(10, "Прихід"))).EnsureSuccessStatusCode();

        // Аркуш з сервера: знаходимо свій рядок і вписуємо пораховане в «Фактично».
        using var book = new XLWorkbook(new MemoryStream(await client.GetByteArrayAsync("/stock/stocktaking/sheet.xlsx")));
        var sheet = book.Worksheets.First();
        Assert.Equal("Фактично", sheet.Cell(1, 6).GetString());
        var row = sheet.RowsUsed().Single(r => r.Cell(1).GetString() == item.ToString());
        Assert.Equal(10, row.Cell(5).GetDouble());
        row.Cell(6).Value = "7,5";
        using var file = new MemoryStream();
        book.SaveAs(file);

        var content = new ByteArrayContent(file.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var preview = await (await client.PostAsync("/stock/stocktaking/preview", content)).Content.ReadFromJsonAsync<StocktakingPreviewDto>();
        Assert.Empty(preview!.Errors);
        var line = Assert.Single(preview.Lines);
        Assert.Equal((10m, 7.5m, -2.5m), (line.Booked, line.Actual, line.Difference));

        var applied = await client.PostAsJsonAsync("/stock/stocktaking", new ApplyStocktakingRequest([new(item, line.Actual)], "жовтень"));
        var result = (await applied.Content.ReadFromJsonAsync<StocktakingResult>())!;
        Assert.Equal((1, 1, -125m), (result.Counted, result.Changed, result.Value));

        var movements = await client.GetFromJsonAsync<List<StockMovementDto>>($"/stock/items/{item}/movements");
        Assert.Equal((-2.5m, "Інвентаризація: жовтень"), (movements![0].Delta, movements[0].Reason));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/stock/stocktaking", new ApplyStocktakingRequest([new(item, -1)]))).StatusCode);
    }

    [MySqlFact]
    public async Task ModelStocktaking_RecountsCutSewnAndComponents()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);
        var modelId = await CreateAsync(client, "/production/models", new SaveProductionModelRequest("Сорочка для інвентаризації", [new("M", 100)]));
        var front = await CreateAsync(client, $"/production/models/{modelId}/components", new SaveComponentRequest("Перед"));
        var sizeM = (await client.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}"))!.Sizes.Single().Id;
        (await client.PostAsJsonAsync($"/production/models/{modelId}/sewn", new ProductionOperationRequest([new(sizeM, 4)]))).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync($"/production/models/{modelId}/stocktaking",
            new ProductionStocktakingRequest([new(sizeM, Cut: 2, Sewn: 3)], [new(front, sizeM, 6)]));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<ProductionStocktakingResult>())!;
        Assert.Equal((1, 1, 1), (result.Cut.Count, result.Sewn.Count, result.Components));
        var model = (await client.GetFromJsonAsync<ProductionModelDto>($"/production/models/{modelId}"))!;
        Assert.Equal((2m, 3m, 6m), (model.Sizes[0].CutStock, model.Sizes[0].SewnStock, model.Components[0].Stock[sizeM]));
    }

    private static async Task<int> CreateAsync<T>(HttpClient client, string url, T body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }
}
