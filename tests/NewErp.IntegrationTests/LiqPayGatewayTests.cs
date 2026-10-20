using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using NewErp.Application.Payments;
using NewErp.Infrastructure.Payments;

namespace NewErp.IntegrationTests;

public class LiqPayGatewayTests
{
    // Ключі вигадані.
    private static readonly LiqPayOptions Options = new() { PublicKey = "pub-test", PrivateKey = "priv-test" };

    [Fact]
    public void CheckoutUrl_HasSignedBase64Data()
    {
        var url = new Uri(new LiqPayGateway(new HttpClient(), Options).CheckoutUrl(new LiqPayCheckout("2045", 300.5m, "UAH", "Оплата замовлення #2045.")));

        Assert.Equal("https://www.liqpay.ua/api/3/checkout", url.GetLeftPart(UriPartial.Path));
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);
        var data = query["data"]!;
        var json = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(data)))!;
        Assert.Equal(("pub-test", "pay", "3", "2045", 300.5m, "UAH", "Оплата замовлення #2045."),
            (json["public_key"]!.GetValue<string>(), json["action"]!.GetValue<string>(), json["version"]!.GetValue<string>(),
             json["order_id"]!.GetValue<string>(), json["amount"]!.GetValue<decimal>(), json["currency"]!.GetValue<string>(),
             json["description"]!.GetValue<string>()));
        Assert.Equal(Signature(data), query["signature"]);
    }

    [Fact]
    public async Task Status_PostsSignedRequest_AndParsesPaidReply()
    {
        var handler = new Handler("""{"result":"ok","status":"success","amount":300.5,"end_date":1792483200000}""");
        var reply = await new LiqPayGateway(new HttpClient(handler), Options).StatusAsync("2045", CancellationToken.None);

        Assert.Equal(("success", (string?)null, 300.5m), (reply.Status, reply.ErrorCode, reply.Amount));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1792483200000).UtcDateTime, reply.EndDate);
        Assert.Equal("https://www.liqpay.ua/api/request", handler.Url);
        var form = System.Web.HttpUtility.ParseQueryString(handler.Body!);
        var request = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(form["data"]!)))!;
        Assert.Equal(("status", "2045"), (request["action"]!.GetValue<string>(), request["order_id"]!.GetValue<string>()));
        Assert.Equal(Signature(form["data"]!), form["signature"]);
    }

    [Fact]
    public void ParseStatus_NotFound() =>
        Assert.Equal("payment_not_found", LiqPayGateway.ParseStatus("""{"result":"error","err_code":"payment_not_found"}""").ErrorCode);

    [Fact]
    public async Task Stub_IsTest_AndNeverPaid()
    {
        var stub = new StubLiqPayGateway();
        Assert.True(stub.IsTest);
        Assert.StartsWith("https://www.liqpay.ua/api/3/checkout?data=", stub.CheckoutUrl(new LiqPayCheckout("1", 10m, "UAH", "x")));
        Assert.Equal("payment_not_found", (await stub.StatusAsync("1", CancellationToken.None)).ErrorCode);
    }

    private static string Signature(string data) =>
        Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(Options.PrivateKey + data + Options.PrivateKey)));

    private sealed class Handler(string response) : HttpMessageHandler
    {
        public string? Url { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.ToString();
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
        }
    }
}
