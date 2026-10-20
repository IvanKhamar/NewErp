using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using NewErp.Application.Locations;

namespace NewErp.IntegrationTests;

/// <summary>Довідник складів. Окрема тестова база, бо тест додає й видаляє записи.</summary>
public class LocationApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Crud_And_ExcelRoundTrip()
    {
        var manager = await factory.LoginAsync(ApiFactory.Manager);

        var response = await manager.PostAsJsonAsync("/locations", new SaveLocationRequest("Склад на Подолі", City: "Київ", Telephone: "0671234567"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<Created>())!.Id;
        var other = (await (await manager.PostAsJsonAsync("/locations", new SaveLocationRequest("Тимчасовий"))).Content.ReadFromJsonAsync<Created>())!.Id;
        (await manager.DeleteAsync($"/locations/{other}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync("/locations", new SaveLocationRequest(" "))).StatusCode);

        // Вивантажений файл після правки назви імпортується назад: запис з тим самим ID оновлюється.
        var file = await manager.GetByteArrayAsync("/locations/export.xlsx");
        using var book = new ClosedXML.Excel.XLWorkbook(new MemoryStream(file));
        var sheet = book.Worksheets.First();
        Assert.Equal(("ID", "0671234567"), (sheet.Cell(1, 1).GetString(), sheet.Cell(2, 5).GetString()));
        sheet.Cell(2, 2).Value = "Склад на Подолі (новий)";
        sheet.Cell(3, 2).Value = "Пункт видачі у Львові";
        sheet.Cell(3, 6).Value = "Львів";
        using var edited = new MemoryStream();
        book.SaveAs(edited);

        var content = new ByteArrayContent(edited.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var result = await (await manager.PostAsync("/locations/import", content)).Content.ReadFromJsonAsync<LocationImportResult>();
        Assert.Equal((1, 1), (result!.Added, result.Updated));

        var list = await manager.GetFromJsonAsync<List<LocationDto>>("/locations");
        Assert.Equal(["Склад на Подолі (новий)", "Пункт видачі у Львові"], list!.Select(l => l.Name));
        Assert.Equal("Київ", list![0].City);

        var junk = new ByteArrayContent("не excel"u8.ToArray());
        junk.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsync("/locations/import", junk)).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/locations")).StatusCode);
    }
}
