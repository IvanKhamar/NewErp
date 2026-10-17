using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Common;
using NewErp.Application.Payments;
using NewErp.Domain.Payments;

namespace NewErp.IntegrationTests;

public class PaymentApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task SuggestLinkHide_Payment()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var unlinked = await client.GetFromJsonAsync<PagedResult<BankPaymentDto>>("/payments/bank?linked=false");
        var payment = Assert.Single(unlinked!.Items);
        Assert.Equal((2000m, 5000m, "Тарас Клієнт2"), (payment.Amount, payment.Balance, payment.Payer));

        var suggestions = await client.GetFromJsonAsync<List<PaymentSuggestionDto>>($"/payments/bank/{payment.Id}/suggestions");
        var best = suggestions![0];
        Assert.Equal(2, best.OrderId);
        // Номер «2» занадто короткий, щоб вважати його номером замовлення (так само «2 шт»), тож збіглися сума й прізвище.
        Assert.Equal(MatchReason.Total | MatchReason.Name, best.Reasons);
        Assert.Equal("5) Відправлено", best.Status);

        var linked = await client.PutAsJsonAsync($"/payments/bank/{payment.Id}/order", new LinkPaymentRequest(2));
        Assert.Equal(2, (await linked.Content.ReadFromJsonAsync<BankPaymentDto>())!.OrderId);
        Assert.Equal(payment.Id, Assert.Single((await client.GetFromJsonAsync<List<BankPaymentDto>>("/orders/2/payments"))!).Id);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"/payments/bank/{payment.Id}/order", new LinkPaymentRequest(999))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/payments/bank/{payment.Id}/visible?visible=false", null)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<PagedResult<BankPaymentDto>>("/payments/bank"))!.Items);
        Assert.Single((await client.GetFromJsonAsync<PagedResult<BankPaymentDto>>("/payments/bank?hidden=true"))!.Items);
    }

    [MySqlFact]
    public async Task Import_WithoutBankKeys_ReportsNotConfigured()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var result = await (await client.PostAsync("/payments/bank/import", null)).Content.ReadFromJsonAsync<BankImportResult>();

        Assert.Equal(new BankImportResult(false, 0, 0, 0), result);
    }
}
