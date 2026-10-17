using System.Net;
using System.Text;

using NewErp.Domain.Common;
using NewErp.Infrastructure.Payments;

namespace NewErp.IntegrationTests;

public class PrivatBankClientTests
{
    // Формат відповіді Автоклієнта, як його розбирав старий сайт (checkBusinessCards). Дані вигадані.
    private const string Statement = """
        {"StatementsResponse": {"statements": [
          {"UA213052990000026001000000001": [
            {"0": {"ID": "tx-1", "TRANTYPE": "C", "BPL_SUM_E": "1500.00", "BPL_DAT_KL": "15.10.2026", "BPL_TIM_P": "14:05",
                   "BPL_OSND": "Оплата зам. 2045", "BPL_A_NAM": "Іваненко Ірина", "BPL_A_ACC": "UA11", "BPL_A_MFO": "305299", "BPL_A_CRF": "1234567890", "BPL_C_BAL": "9000.50"}},
            {"1": {"ID": "tx-2", "TRANTYPE": "D", "BPL_SUM_E": "200", "BPL_DAT_KL": "15.10.2026", "BPL_TIM_P": "15:00", "BPL_OSND": "Комісія"}}
          ]}
        ]}}
        """;

    [Fact]
    public void Parse_ReadsIncomingAndDebit()
    {
        var transactions = PrivatBankClient.Parse(Statement);

        Assert.Equal(2, transactions.Count);
        var t = transactions[0];
        Assert.Equal(("tx-1", "UA213052990000026001000000001", true, 1500m, 9000.5m), (t.Id, t.Account, t.IsIncoming, t.Amount, t.Balance));
        Assert.Equal((new DateOnly(2026, 10, 15), new TimeOnly(14, 5)), (t.Date, t.Time));
        Assert.Equal(("Оплата зам. 2045", "Іваненко Ірина", "305299"), (t.Purpose, t.Payer, t.PayerBankCode));
        Assert.False(transactions[1].IsIncoming);
    }

    [Fact]
    public async Task Fetch_SendsIdAndTokenHeaders_ForEachAccount_AndDecodesWindows1251()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var handler = new Handler(Encoding.GetEncoding(1251).GetBytes(Statement));
        var client = new PrivatBankClient(new HttpClient(handler), new PrivatBankOptions { Accounts = [new() { Id = "client-id", Token = "secret" }] });

        var transactions = await client.FetchAsync(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 16), CancellationToken.None);

        Assert.Equal("Оплата зам. 2045", transactions[0].Purpose);
        Assert.Equal("https://acp.privatbank.ua/api/proxy/transactions?startDate=15-10-2026&endDate=16-10-2026", handler.Url);
        Assert.Equal(("client-id", "secret"), (handler.Id, handler.Token));
    }

    [Fact]
    public void Parse_UnknownFormat_IsDomainError() =>
        Assert.Throws<DomainException>(() => PrivatBankClient.Parse("""{"error": "Unauthorized"}"""));

    private sealed class Handler(byte[] body) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Id { get; private set; }
        public string? Token { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.ToString();
            Id = request.Headers.GetValues("id").Single();
            Token = request.Headers.GetValues("token").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        }
    }
}
