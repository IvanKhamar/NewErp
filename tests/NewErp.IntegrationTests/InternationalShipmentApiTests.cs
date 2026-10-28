using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Delivery;
using NewErp.Domain.Delivery;

namespace NewErp.IntegrationTests;

/// <summary>
/// Накладні за кордон на заглушці (NovaPostInternational:FakeDocuments, без ключа API), перенесено з Java
/// InternationalShipmentControllerTest. Замовлення ApiFactory.OrderId ще не має ТТН.
/// </summary>
public class InternationalShipmentApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly CreateInternationalShipmentRequest Create = new(
        new("Anna", "Kowalska", "+48 600 100 200", "anna@example.com", "PL", null, "Kraków", "Floriańska", "5", null, "31-019"), null,
        [new("Сукня лляна", "Linen dress", "6204.49.10", 450, 1m, 1500m)], [new ShipmentSeat(1, 31, 36, 6)], 1500m, "UAH");

    [MySqlFact]
    public async Task Form_Countries_HsCodes_Create_SkippedByTracking_Delete_AndRefusals()
    {
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        var orderShipment = $"/orders/{ApiFactory.OrderId}/shipment";

        var form = (await manager.GetFromJsonAsync<InternationalFormDto>($"{orderShipment}/international/form"))!;
        Assert.Equal(("Тарас", true, true, "DAP"), (form.Recipient.FirstName, form.CanCreate, form.Test, form.Terms.Incoterm));
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync("/orders/999/shipment/international/form")).StatusCode);

        var countries = (await manager.GetFromJsonAsync<List<CountryDto>>("/delivery/international/countries"))!;
        Assert.Contains(new CountryDto("PL", "Польща"), countries);
        Assert.DoesNotContain(countries, c => c.Code == "UA");
        Assert.Equal("6204491000",
            (await manager.GetFromJsonAsync<List<HsCodeDto>>("/delivery/international/hs-codes?search=су&country=PL"))![0].HsCode);
        Assert.Empty((await manager.GetFromJsonAsync<List<HsCodeDto>>("/delivery/international/hs-codes?search=с"))!);

        var created = await manager.PostAsJsonAsync($"{orderShipment}/international", Create);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var shipment = (await created.Content.ReadFromJsonAsync<ShipmentDto>())!;
        Assert.Equal((ApiFactory.OrderId, ShipmentDocument.International, 1500m), (shipment.OrderId, shipment.Carrier, shipment.Cost));
        Assert.StartsWith("TEST", shipment.Number);
        Assert.Equal(ShipmentDocument.International, (await manager.GetFromJsonAsync<ShipmentDto>(orderShipment))!.Carrier);

        // Внутрішнє відстеження не має питати про неї внутрішній API.
        using (var scope = factory.Services.CreateScope())
        {
            var open = await scope.ServiceProvider.GetRequiredService<IShipmentRepository>().ListOpenForTrackingAsync(default);
            Assert.DoesNotContain(open, s => s.Document.OrderId == ApiFactory.OrderId);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync($"{orderShipment}/international", Create)).StatusCode); // одна на замовлення
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync(orderShipment)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.GetAsync(orderShipment)).StatusCode);

        var incomplete = await manager.PostAsJsonAsync($"{orderShipment}/international",
            new CreateInternationalShipmentRequest(new(null, null, null, null, "UA", null, null, null, null, null, null), null, [], [], null, null));
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        var detail = JsonDocument.Parse(await incomplete.Content.ReadAsStringAsync()).RootElement.GetProperty("detail").GetString();
        Assert.StartsWith("Не заповнені або некоректні поля: Прізвище", detail);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync("/orders/999/shipment/international", Create)).StatusCode);

        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.PostAsJsonAsync($"{orderShipment}/international", Create)).StatusCode);
    }
}
