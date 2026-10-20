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

    [MySqlFact]
    public async Task LiqPay_WithoutKeys_GivesTestLink_AndNotPaid()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var link = await (await client.PostAsJsonAsync("/orders/2/liqpay-link", new CreatePaymentLinkRequest(500m))).Content.ReadFromJsonAsync<PaymentLinkDto>();
        Assert.Equal((500m, "UAH", true), (link!.Amount, link.Currency, link.IsTest));
        Assert.StartsWith("https://www.liqpay.ua/api/3/checkout?data=", link.Url);

        var status = await client.GetFromJsonAsync<LiqPayStatusDto>("/orders/2/liqpay-status");
        Assert.Equal(LiqPayState.NotFound, status!.State);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/orders/2/liqpay-link", new CreatePaymentLinkRequest(2000.01m))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/orders/999/liqpay-link", new CreatePaymentLinkRequest(null))).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/orders/2/liqpay-status")).StatusCode);
    }
}
