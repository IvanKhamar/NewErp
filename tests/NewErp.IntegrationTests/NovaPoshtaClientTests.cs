using System.Net;
using System.Text;
using System.Text.Json;

using NewErp.Application.Delivery;
using NewErp.Infrastructure.Delivery;

namespace NewErp.IntegrationTests;

/// <summary>
/// Справжній клієнт Нової Пошти без мережі: HttpClient отримує підставний обробник,
/// який перевіряє запит і повертає відповідь у форматі API.
/// </summary>
public class NovaPoshtaClientTests
{
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
        var client = new NovaPoshtaClient(new HttpClient(handler), new NovaPoshtaOptions { ApiKey = "test-key" });

        var infos = await client.TrackAsync([new TrackingRequest("20450000000001", "380670000001")], CancellationToken.None);

        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal("getStatusDocuments", request.RootElement.GetProperty("calledMethod").GetString());
        var document = request.RootElement.GetProperty("methodProperties").GetProperty("Documents")[0];
        Assert.Equal("380670000001", document.GetProperty("Phone").GetString());

        Assert.Equal(2, infos.Count);
        Assert.Equal((7, true), (infos[0].StatusCode, infos[0].Redelivery));
        Assert.Equal(new DateTime(2026, 10, 15, 10, 30, 0), infos[0].DateCreated);
        Assert.Equal(new DateOnly(2026, 10, 22), infos[0].DatePayedKeeping);
        Assert.Equal((9, "", null), (infos[1].StatusCode, infos[1].Status, infos[1].DateCreated));
    }

    [Fact]
    public async Task Track_ApiError_Throws()
    {
        var client = new NovaPoshtaClient(new HttpClient(new FakeHandler("""{"success": false, "errors": ["API key expired"]}""")), new NovaPoshtaOptions());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.TrackAsync([new TrackingRequest("20450000000001", "")], CancellationToken.None));
        Assert.Contains("API key expired", error.Message);
    }

    private sealed class FakeHandler(string response) : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
