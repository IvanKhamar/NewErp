using System.Net;
using System.Text;
using System.Text.Json;

using NewErp.Domain.Cashbox;
using NewErp.Domain.Common;
using NewErp.Infrastructure.Cashbox;

namespace NewErp.IntegrationTests;

/// <summary>Справжній клієнт Checkbox без мережі: підставний обробник запам'ятовує запити й повертає відповіді у форматі API.</summary>
public class CheckboxClientTests
{
    private static readonly CheckboxOptions Options = new()
    {
        BaseUrl = "https://checkbox.test/",
        Cashiers = [new() { CashierId = 1, Login = "olena", Password = "pin", LicenseKey = "license-1" }],
    };

    private static CheckboxClient Client(Handler handler) => new(new HttpClient(handler), Options, TimeSpan.Zero);

    private const string SignIn = """{"access_token": "token-1"}""";

    [Fact]
    public async Task Sell_SignsInOnce_SendsReceiptInKopecks()
    {
        var handler = new Handler((HttpStatusCode.OK, SignIn), (HttpStatusCode.Created, """{"id": "r-1"}"""), (HttpStatusCode.Created, """{"id": "r-2"}"""));
        var client = Client(handler);
        var receipt = FiscalReceipt.Build(42, "Олена", [new ReceiptSourceLine(101, "Сукня", 750m, 2)], 1200m, "cod", "", "a@b.ua");

        Assert.Equal("r-1", await client.SellAsync(1, receipt, CancellationToken.None));
        Assert.Equal("r-2", await client.SellAsync(1, receipt, CancellationToken.None));

        Assert.Equal(["/api/v1/cashier/signin", "/api/v1/receipts/sell", "/api/v1/receipts/sell"], handler.Paths);
        Assert.Equal("Bearer token-1", handler.Auth[1]);
        var body = handler.Json(1);
        var good = body.GetProperty("goods")[0];
        Assert.Equal((75000, 2000), (good.GetProperty("good").GetProperty("price").GetInt64(), good.GetProperty("quantity").GetInt64()));
        Assert.Equal(("CASHLESS", 120000L), (body.GetProperty("payments")[0].GetProperty("type").GetString(), body.GetProperty("payments")[0].GetProperty("value").GetInt64()));
        Assert.Equal(30000, body.GetProperty("discounts")[0].GetProperty("value").GetInt64());
        Assert.Equal("a@b.ua", body.GetProperty("delivery").GetProperty("email").GetString());
        Assert.Equal("NewErp", handler.ClientNames[1]);
    }

    [Fact]
    public async Task ExpiredToken_SignsInAgain_AndRepeats()
    {
        var handler = new Handler(
            (HttpStatusCode.OK, SignIn), (HttpStatusCode.Unauthorized, """{"message": "Invalid token"}"""),
            (HttpStatusCode.OK, """{"access_token": "token-2"}"""), (HttpStatusCode.Created, """{"id": "x-1"}"""));

        Assert.Equal("x-1", await Client(handler).CreateXReportAsync(1, CancellationToken.None));
        Assert.Equal("Bearer token-2", handler.Auth[3]);
    }

    [Fact]
    public async Task OpenShift_TakesAlreadyOpen_OtherwiseCreatesWithLicenseKey()
    {
        var open = new Handler((HttpStatusCode.OK, SignIn), (HttpStatusCode.OK, """{"id": "s-open", "status": "OPENED"}"""));
        Assert.Equal("s-open", await Client(open).OpenShiftAsync(1, CancellationToken.None));

        var none = new Handler((HttpStatusCode.OK, SignIn), (HttpStatusCode.OK, "null"), (HttpStatusCode.Accepted, """{"id": "s-new", "status": "CREATED"}"""));
        Assert.Equal("s-new", await Client(none).OpenShiftAsync(1, CancellationToken.None));
        Assert.Equal("/api/v1/shifts", none.Paths[2]);
        Assert.Equal("license-1", none.Licenses[2]);
    }

    [Fact]
    public async Task CloseShift_WaitsForZReport()
    {
        var handler = new Handler(
            (HttpStatusCode.OK, SignIn),
            (HttpStatusCode.Accepted, """{"id": "s-1", "status": "CLOSING", "z_report": null}"""),
            (HttpStatusCode.OK, """{"id": "s-1", "status": "CLOSING", "z_report": null}"""),
            (HttpStatusCode.OK, """{"id": "s-1", "status": "CLOSED", "z_report": {"id": "z-1"}}"""));

        Assert.Equal("z-1", await Client(handler).CloseShiftAsync(1, CancellationToken.None));
        Assert.Equal("/api/v1/shifts/s-1", handler.Paths[3]);
    }

    [Fact]
    public async Task Refusal_BecomesDomainErrorWithCheckboxMessage()
    {
        var handler = new Handler((HttpStatusCode.OK, SignIn), (HttpStatusCode.BadRequest, """{"message": "Зміну не відкрито"}"""));

        var error = await Assert.ThrowsAsync<DomainException>(() => Client(handler).CreateXReportAsync(1, CancellationToken.None));
        Assert.Contains("Зміну не відкрито", error.Message);
    }

    [Fact]
    public void UnknownCashier_CannotBeUsed_ReceiptUrlHasNoSecrets()
    {
        var client = Client(new Handler());

        Assert.False(client.CanUse(2));
        Assert.Equal("https://checkbox.test/api/v1/receipts/r-1/html", client.ReceiptUrl("r-1"));
    }

    private sealed class Handler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new(responses);

        public List<string> Paths { get; } = [];
        public List<string?> Auth { get; } = [];
        public List<string?> Licenses { get; } = [];
        public List<string?> ClientNames { get; } = [];
        public List<string> Bodies { get; } = [];

        public JsonElement Json(int index) => JsonDocument.Parse(Bodies[index]).RootElement;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            Auth.Add(request.Headers.Authorization?.ToString());
            Licenses.Add(request.Headers.TryGetValues("X-License-Key", out var l) ? l.Single() : null);
            ClientNames.Add(request.Headers.TryGetValues("X-Client-Name", out var n) ? n.Single() : null);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            var (status, body) = _responses.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
