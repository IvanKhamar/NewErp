using System.Net;
using System.Text;
using System.Text.Json;

using NewErp.Application.Delivery;
using NewErp.Domain.Common;
using NewErp.Domain.Delivery;
using NewErp.Infrastructure.Delivery;

namespace NewErp.IntegrationTests;

/// <summary>
/// Справжній клієнт Нової Пошти без мережі: HttpClient отримує підставний обробник,
/// який запам'ятовує запити й повертає відповіді у форматі API.
/// </summary>
public class NovaPoshtaClientTests
{
    private static readonly NovaPoshtaOptions Options = new()
    {
        Senders =
        [
            new() { Id = "fop-a", Name = "ФОП А", ApiKey = "key-a", CounterpartyRef = "cp-a", ContactRef = "contact-a", Phone = "380501112233" },
            new() { Id = "fop-b", Name = "ФОП Б", ApiKey = "key-b", CounterpartyRef = "cp-b", ContactRef = "contact-b", Phone = "380504445566", IsDefault = true },
        ],
        DropoffPoints = [new() { Id = "kyiv", Name = "Київ, №5", CityRef = "city-sender", AddressRef = "wh-sender", IsDefault = true }],
    };

    private static NovaPoshtaClient Client(FakeHandler handler, NovaPoshtaOptions? options = null) =>
        new(new HttpClient(handler), options ?? Options);

    [Fact]
    public async Task Track_SendsNumbersWithPhones_AndParsesMixedFormats()
    {
        var handler = new FakeHandler("""
            {"success": true, "data": [
              {"Number": "20450000000001", "Status": "Прибув у відділення", "StatusCode": "7", "RecipientAddress": "Київ, №1",
               "Redelivery": 1, "DateCreated": "15-10-2026 10:30:00", "ScheduledDeliveryDate": "2026-10-17 12:00:00", "DatePayedKeeping": "2026-10-22 00:00:00"},
              {"Number": "20450000000002", "StatusCode": 9}
            ]}
            """);

        var infos = await Client(handler).TrackAsync([new TrackingRequest("20450000000001", "380670000001")], CancellationToken.None);

        var request = handler.Json(0);
        Assert.Equal("key-b", request.GetProperty("apiKey").GetString()); // без окремого ключа — ключ типового відправника
        Assert.Equal("getStatusDocuments", request.GetProperty("calledMethod").GetString());
        var document = request.GetProperty("methodProperties").GetProperty("Documents")[0];
        Assert.Equal("380670000001", document.GetProperty("Phone").GetString());

        Assert.Equal(2, infos.Count);
        Assert.Equal((7, true), (infos[0].StatusCode, infos[0].Redelivery));
        Assert.Equal(new DateTime(2026, 10, 15, 10, 30, 0), infos[0].DateCreated);
        Assert.Equal(new DateOnly(2026, 10, 22), infos[0].DatePayedKeeping);
        Assert.Equal((9, "", null), (infos[1].StatusCode, infos[1].Status, infos[1].DateCreated));
    }

    [Fact]
    public async Task Track_ApiError_BecomesDomainError()
    {
        var handler = new FakeHandler("""{"success": false, "errors": ["API key expired"]}""");

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            Client(handler).TrackAsync([new TrackingRequest("20450000000001", "")], CancellationToken.None));
        Assert.Contains("API key expired", error.Message);
    }

    [Fact]
    public async Task Create_SavesRecipientThenDocument_WithSenderKey()
    {
        var handler = new FakeHandler(
            """{"success": true, "data": [{"Ref": "recipient-ref", "ContactPerson": {"data": [{"Ref": "contact-ref"}]}}]}""",
            """{"success": true, "data": [{"Ref": "doc-ref", "IntDocNumber": "20450123456789", "CostOnSite": 85}]}""");
        var shipment = new NewShipment(
            "fop-a", "kyiv", 291, new DateOnly(2026, 10, 16), "Тарас", "Шевченко", "", "380671234567", "city-kyiv", "wh-1",
            1500, 1200, false, [new ShipmentSeat(1.5m, 31, 36, 6), new ShipmentSeat(2, 41, 48, 8)]);

        var created = await Client(handler).CreateAsync(shipment, CancellationToken.None);

        Assert.Equal(new CreatedShipment("doc-ref", "20450123456789", 85), created);
        var counterparty = handler.Json(0);
        Assert.Equal(("key-a", "Counterparty", "save"), Call(counterparty));
        Assert.Equal("Recipient", counterparty.GetProperty("methodProperties").GetProperty("CounterpartyProperty").GetString());

        var document = handler.Json(1);
        Assert.Equal(("key-a", "InternetDocument", "save"), Call(document));
        var p = document.GetProperty("methodProperties");
        Assert.Equal(
            ("cp-a", "contact-a", "wh-sender", "recipient-ref", "contact-ref", "wh-1"),
            (S(p, "Sender"), S(p, "ContactSender"), S(p, "SenderAddress"), S(p, "Recipient"), S(p, "ContactRecipient"), S(p, "RecipientAddress")));
        Assert.Equal(("Recipient", "16.10.2026", "2", "3.5", "1200", "291"),
            (S(p, "PayerType"), S(p, "DateTime"), S(p, "SeatsAmount"), S(p, "Weight"), S(p, "AfterpaymentOnGoodsCost"), S(p, "InfoRegClientBarcodes")));
        Assert.Equal("0.006696", S(p.GetProperty("OptionsSeat")[0], "volumetricVolume"));
    }

    [Fact]
    public async Task Create_WithoutCashOnDelivery_DoesNotSendAfterpayment()
    {
        var handler = new FakeHandler(
            """{"success": true, "data": [{"Ref": "r", "ContactPerson": {"data": [{"Ref": "c"}]}}]}""",
            """{"success": true, "data": [{"Ref": "d", "IntDocNumber": "20450123456789", "CostOnSite": "70"}]}""");
        var shipment = new NewShipment(
            "fop-b", "kyiv", 1, new DateOnly(2026, 10, 16), "А", "Б", "", "380671234567", "c", "w", 100, 0, true, [new ShipmentSeat(1, 0, 0, 0)]);

        await Client(handler).CreateAsync(shipment, CancellationToken.None);

        var p = handler.Json(1).GetProperty("methodProperties");
        Assert.False(p.TryGetProperty("AfterpaymentOnGoodsCost", out _));
        Assert.Equal("Sender", S(p, "PayerType"));
    }

    [Fact]
    public async Task Print_ReturnsPdf_AndRejectsAnythingElse()
    {
        var pdf = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("%PDF-1.4 ..."u8.ToArray()) });
        var html = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>Помилка</html>") });

        var bytes = await Client(pdf).PrintAsync("fop-a", "doc-ref", ShipmentPrintKind.Markings, CancellationToken.None);

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes));
        Assert.Contains("/printMarkings/orders[]/doc-ref/type/pdf/zebra/zebra/apiKey/key-a", pdf.Urls[0]);
        await Assert.ThrowsAsync<DomainException>(() => Client(html).PrintAsync("fop-a", "doc-ref", ShipmentPrintKind.Document, CancellationToken.None));
    }

    [Fact]
    public async Task FetchWarehouses_ReadsPagesUntilEmpty()
    {
        var handler = new FakeHandler(
            """{"success": true, "data": [{"Ref": "w1", "Description": "Відділення №1", "CategoryOfWarehouse": "Branch", "CityRef": "c", "CityDescription": "Київ", "TotalMaxWeightAllowed": "30", "WarehouseStatus": "Working"}]}""",
            """{"success": true, "data": [{"Ref": "w2", "Description": "Поштомат №2", "CategoryOfWarehouse": "Postomat", "CityRef": "c", "CityDescription": "Київ", "TotalMaxWeightAllowed": 20, "WarehouseStatus": "Closed"}]}""",
            """{"success": true, "data": []}""");

        var warehouses = await Client(handler).FetchWarehousesAsync(CancellationToken.None);

        Assert.Equal(3, handler.Bodies.Count);
        Assert.Equal(3, handler.Json(2).GetProperty("methodProperties").GetProperty("Page").GetInt32());
        Assert.Equal([("w1", 1, 30, true), ("w2", 2, 20, false)], warehouses.Select(w => (w.Ref, w.SortOrder, w.MaxWeight, w.Status)));
    }

    [Fact]
    public void IncompleteSender_FailsAtStartup() =>
        Assert.Throws<InvalidOperationException>(() => Client(new FakeHandler(), new NovaPoshtaOptions
        {
            Senders = [new() { Id = "fop-a", ApiKey = "key" }],
        }));

    private static (string?, string?, string?) Call(JsonElement request) =>
        (S(request, "apiKey"), S(request, "modelName"), S(request, "calledMethod"));

    private static string? S(JsonElement element, string name) => element.GetProperty(name).GetString();

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public FakeHandler(params string[] jsonResponses)
        {
            foreach (var json in jsonResponses)
                _responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }

        public FakeHandler(HttpResponseMessage response) => _responses.Enqueue(() => response);

        public List<string> Bodies { get; } = [];

        public List<string> Urls { get; } = [];

        public JsonElement Json(int index) => JsonDocument.Parse(Bodies[index]).RootElement;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.ToString());
            if (request.Content is not null)
                Bodies.Add(await request.Content.ReadAsStringAsync(ct));
            return _responses.Dequeue()();
        }
    }
}
