using System.Net;
using System.Text;
using System.Text.Json;

using NewErp.Application.Delivery;
using NewErp.Domain.Common;
using NewErp.Domain.Delivery;
using NewErp.Infrastructure.Delivery;

namespace NewErp.IntegrationTests;

/// <summary>HTTP-виклики на підробленому сервері, перенесено з Java HttpNovaPostInternationalClientTest; компанія й відповіді вигадані.</summary>
public class NovaPostInternationalClientTests
{
    private const string Api = "https://npi.test/v.1.0";

    private static readonly NovaPostInternationalOptions Options = new()
    {
        ApiKey = "secret-key", BaseUrl = Api,
        Sender = new() { CompanyTin = "12345678", CompanyName = "ТОВ Тест", Name = "Іван Тестовий", Phone = "+380 50 000 00 00",
            Email = "shop@example.com", City = "Київ", Street = "вул. Тестова", Building = "1", PostCode = "01001" },
    };

    private static readonly InternationalShipment Shipment = InternationalShipment.Of(1001,
        new("Anna", "Kowalska", "48600100200", "anna@example.com", "PL", "", "Kraków", "Floriańska", "5", "2", "31-019"), null,
        [new("Сукня лляна", "Linen dress", "6204491000", 450, 1m, 3000m)], [SeatPreset.All[0].Seat], 3000m, "UAH");

    [Fact]
    public async Task Create_SendsTheJwtAsIs_AndTheOldSitesPayload()
    {
        var handler = new Handler((HttpStatusCode.OK, """{"jwt":"token-1"}"""),
            (HttpStatusCode.Created, """{"id":"sh-77","number":"NP000123456UA","status":"ReadyToShip","cost":812.5}"""));

        var created = await new NovaPostInternationalClient(new HttpClient(handler), Options).CreateAsync(Shipment, default);

        Assert.Equal(new CreatedShipment("sh-77", "NP000123456UA", 812.5m), created);
        Assert.Equal(["/v.1.0/clients/authorization?apiKey=secret-key", "/v.1.0/shipments"], handler.Paths);
        Assert.Equal("token-1", handler.Auth[1]);
        var body = handler.Json(1);
        Assert.Equal(("ReadyToShip", "1001", "Sender"), (body.GetProperty("status").GetString(), body.GetProperty("clientOrder").GetString(),
            body.GetProperty("payerType").GetString()));
        var parcel = body.GetProperty("parcels")[0];
        Assert.Equal((310, 360, 60, 450, 3000m), (parcel.GetProperty("width").GetInt32(), parcel.GetProperty("length").GetInt32(),
            parcel.GetProperty("height").GetInt32(), parcel.GetProperty("actualWeight").GetInt32(), parcel.GetProperty("insuranceCost").GetDecimal()));
        var sender = body.GetProperty("sender");
        Assert.Equal(("380500000000", "UA", "01001"), (sender.GetProperty("phone").GetString(), sender.GetProperty("countryCode").GetString(),
            sender.GetProperty("registrationAddressParts").GetProperty("postCode").GetString()));
        Assert.Equal(("Anna Kowalska", "PL", "2"), (body.GetProperty("recipient").GetProperty("name").GetString(),
            body.GetProperty("recipient").GetProperty("countryCode").GetString(),
            body.GetProperty("recipient").GetProperty("addressParts").GetProperty("flat").GetString()));
        Assert.Equal(("DAP", "6204491000"), (body.GetProperty("invoice").GetProperty("incoterm").GetString(),
            body.GetProperty("invoice").GetProperty("items")[0].GetProperty("hsCode").GetString()));
    }

    [Fact]
    public async Task TakesANewJwtOnce_WhenTheOldOneExpired()
    {
        var handler = new Handler((HttpStatusCode.OK, """{"jwt":"token-1"}"""),
            (HttpStatusCode.OK, """{"items":[{"hsCode":"6204491000","product":{"currentLocal":"Сукня","en":"Dress"},"subCategory":{"currentLocal":"З льону"}}]}"""),
            (HttpStatusCode.Unauthorized, ""), (HttpStatusCode.OK, """{"jwt":"token-2"}"""), (HttpStatusCode.OK, """{"items":[]}"""));
        var client = new NovaPostInternationalClient(new HttpClient(handler), Options);

        Assert.Equal([new HsCodeDto("6204491000", "Сукня — З льону", "Dress")], await client.SearchHsCodesAsync("dress", "PL", default));
        Assert.Empty(await client.SearchHsCodesAsync("dress", "PL", default));
        Assert.Equal(("token-1", "token-1", "token-2"), (handler.Auth[1], handler.Auth[2], handler.Auth[4]));
        Assert.Contains("keyword=dress", handler.Queries[4]);
        Assert.Contains("country-code=PL", handler.Queries[4]);
    }

    [Fact]
    public async Task ApiErrors_BecomeAReadableMessage()
    {
        var handler = new Handler((HttpStatusCode.OK, """{"jwt":"token-1"}"""), (HttpStatusCode.UnprocessableContent,
            """{"errors":{"recipient.addressParts.postCode":"Invalid postcode","invoice.items.0.hsCode":"Unknown code"}}"""));

        var error = await Assert.ThrowsAsync<DomainException>(() => new NovaPostInternationalClient(new HttpClient(handler), Options).CreateAsync(Shipment, default));

        Assert.Equal("Nova Post International відмовила (HTTP 422): recipient.addressParts.postCode: Invalid postcode; "
            + "invoice.items.0.hsCode: Unknown code", error.Message);
    }

    [Fact]
    public async Task ARefusedKey_IsSaidSo_AndDeleteUsesTheShipmentId()
    {
        var refused = new Handler((HttpStatusCode.Unauthorized, ""));
        Assert.Equal("Nova Post International не прийняла ключ API (HTTP 401).",
            (await Assert.ThrowsAsync<DomainException>(() => new NovaPostInternationalClient(new HttpClient(refused), Options).DeleteAsync("sh-77", default))).Message);

        var handler = new Handler((HttpStatusCode.OK, """{"jwt":"token-1"}"""), (HttpStatusCode.OK, ""));
        await new NovaPostInternationalClient(new HttpClient(handler), Options).DeleteAsync("sh-77", default);
        Assert.Equal(("/v.1.0/shipments/sh-77", "DELETE", "token-1"), (handler.Paths[1], handler.Methods[1], handler.Auth[1]));
    }

    private sealed class Handler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new(responses);

        public List<string> Paths { get; } = [];
        public List<string> Queries { get; } = [];
        public List<string> Methods { get; } = [];
        public List<string?> Auth { get; } = [];
        public List<string> Bodies { get; } = [];

        public JsonElement Json(int index) => JsonDocument.Parse(Bodies[index]).RootElement;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            // Шлях з рядком запиту для авторизації (там ключ), окремо — рядок запиту для класифікатора.
            Paths.Add(uri.AbsolutePath.EndsWith("/authorization") ? uri.PathAndQuery : uri.AbsolutePath);
            Queries.Add(Uri.UnescapeDataString(uri.Query));
            Methods.Add(request.Method.Method);
            Auth.Add(request.Headers.TryGetValues("Authorization", out var a) ? a.Single() : null);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            var (status, body) = _responses.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
