using System.Net;
using System.Text.Json.Nodes;

using NewErp.Infrastructure.Sms;

namespace NewErp.IntegrationTests;

public class SmsClubSenderTests
{
    // Відповідь SMSClub API v2 на вдале надсилання; id і номер вигадані.
    private const string Accepted = """{"success_request":{"info":{"1092":"380670000001"}}}""";

    [Fact]
    public async Task Send_PostsJsonWithBearerToken_AndReadsAcceptedNumber()
    {
        var handler = new Handler(HttpStatusCode.OK, Accepted);
        var sender = new SmsClubSender(new HttpClient(handler), new SmsClubOptions { Token = "test-token" });

        var result = await sender.SendAsync("380670000001", "Ваше замовлення прийнято", CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Equal(Accepted, result.Response);
        Assert.Equal(("https://im.smsclub.mobi/sms/send", "Bearer test-token"), (handler.Url, handler.Authorization));
        var body = JsonNode.Parse(handler.Body!)!;
        Assert.Equal(("380670000001", "Ваше замовлення прийнято", "Vidro.ua"),
            (body["phone"]![0]!.GetValue<string>(), body["message"]!.GetValue<string>(), body["src_addr"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData("""{"success_request":{"info":[],"add_info":{"380670000001":"Неправильний номер"}}}""")]
    [InlineData("""{"error":"Unauthorized"}""")]
    [InlineData("not json")]
    public async Task Send_RejectedOrUnknownReply_NotSent_ButKeepsResponse(string reply)
    {
        var sender = new SmsClubSender(new HttpClient(new Handler(HttpStatusCode.OK, reply)), new SmsClubOptions { Token = "t" });

        var result = await sender.SendAsync("380670000001", "Текст", CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal(reply, result.Response);
    }

    [Fact]
    public async Task Send_PhoneOutsideOnlyPhones_IsNotRequested()
    {
        var handler = new Handler(HttpStatusCode.OK, Accepted);
        var sender = new SmsClubSender(new HttpClient(handler), new SmsClubOptions { Token = "t", OnlyPhones = ["380990000000"] });

        var result = await sender.SendAsync("380670000001", "Текст", CancellationToken.None);

        Assert.Equal((false, SmsClubSender.NotAllowedResponse), (result.Sent, result.Response));
        Assert.Null(handler.Url);
    }

    private sealed class Handler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public string? Url { get; private set; }

        public string? Authorization { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(response) };
        }
    }
}
