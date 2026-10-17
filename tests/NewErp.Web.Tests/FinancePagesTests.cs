using System.Net;

using NewErp.Application.Cashbox;
using NewErp.Application.Common;
using NewErp.Application.Payments;
using NewErp.Domain.Payments;
using NewErp.Web.Pages.Finance;

namespace NewErp.Web.Tests;

public class FinancePagesTests : WebTestContext
{
    private static readonly BankPaymentDto Payment = new(
        5, "tx-5", "UA00", 1500m, 9000m, "Іваненко Ірина", "Оплата зам. 2045", new DateTime(2026, 10, 15, 14, 5, 0), 0, null, true);

    public FinancePagesTests()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("Менеджер");
        auth.SetRoles("Manager");
    }

    [Fact]
    public void Cashbox_ShowsKeysAndShift_OpensShift()
    {
        Api.Respond("/cashbox/setup", HttpStatusCode.OK, new CashboxSetupDto(
            [
                new CashierDto(1, "Олена", "", "", null, 0, null, 0, null, true, true, null, null),
                new CashierDto(2, "Марта", "", "cod", new TimeOnly(21, 0), 0, null, 0, null, true, false, null, null),
            ],
            [], [], FakeMode: true));
        Api.Respond("/cashbox/cheques", HttpStatusCode.OK, new PagedResult<ChequeDto>([], 1, 50, 0));
        Api.Respond("/cashbox/shifts", HttpStatusCode.OK, new List<WorkShiftDto>());
        Api.Respond("/cashbox/reports", HttpStatusCode.OK, new List<ZReportDto>());
        Api.Respond("/cashbox/cashiers/1/shift/open", HttpStatusCode.OK, new WorkShiftDto(1, 1, "Олена", "s", null, DateTime.Now, null, 0, 0));

        var page = Render<Cashbox>();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-test=fake-mode]")));
        Assert.Contains("немає ключів Checkbox", page.Markup);
        Assert.Contains("автозакриття о 21:00", page.Markup);
        var buttons = page.FindAll("[data-test=open-shift]");
        Assert.True(buttons[1].HasAttribute("disabled")); // у Марти немає ключів

        buttons[0].Click();
        page.WaitForAssertion(() => Assert.Contains("/cashbox/cashiers/1/shift/open", Api.Requests));
    }

    [Fact]
    public void Payments_MatchDialog_ShowsReasons_AndLinks()
    {
        Api.Respond("/payments/bank", HttpStatusCode.OK, new PagedResult<BankPaymentDto>([Payment], 1, 50, 1));
        Api.Respond("/payments/bank/5/suggestions", HttpStatusCode.OK, new List<PaymentSuggestionDto>
        {
            new(2045, "Ірина Іваненко", "+380670000099", 1500m, 0, new DateTime(2026, 10, 1), "2) Прийнято", null,
                MatchReason.OrderNumber | MatchReason.Total, 75),
        });
        Api.Respond("/payments/bank/5/order", HttpStatusCode.OK, Payment with { OrderId = 2045 });

        var provider = Render<MudBlazor.MudDialogProvider>();
        var page = Render<BankPayments>();
        page.WaitForElement("[data-test=match]").Click();

        provider.WaitForAssertion(() => Assert.Contains("номер у призначенні", provider.Markup));
        Assert.Contains("сума", provider.Markup);
        provider.Find("[data-test=link]").Click();

        page.WaitForAssertion(() => Assert.Contains("\"orderId\":2045", Api.Bodies["/payments/bank/5/order"]));
    }

    [Fact]
    public void Payments_ImportWithoutKeys_Explains()
    {
        Api.Respond("/payments/bank", HttpStatusCode.OK, new PagedResult<BankPaymentDto>([], 1, 50, 0));
        Api.Respond("/payments/bank/import", HttpStatusCode.OK, new BankImportResult(false, 0, 0, 0));

        var page = Render<BankPayments>();
        page.WaitForElement("[data-test=import]").Click();

        page.WaitForAssertion(() => Assert.Contains("Ключі ПриватБанку не задано", page.Find("[data-test=import-result]").TextContent));
    }
}
