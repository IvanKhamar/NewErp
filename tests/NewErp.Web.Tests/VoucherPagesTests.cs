using System.Net;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Common;
using NewErp.Application.Marketing;
using NewErp.Application.Sms;
using NewErp.Web.Api;
using NewErp.Web.Pages.Marketing;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

/// <summary>Сторінка й діалог сертифікатів з підміненим API, як VouchersViewTest у Java-версії. Сертифікати вигадані.</summary>
public class VoucherPagesTests : WebTestContext
{
    private static readonly VoucherCardDto Gift = new(1, 0, "GIFT1000", "Магазин", "", "Олена", "", 0, "", "", 1000m, 650m, true, true,
        new DateTime(2026, 9, 1, 10, 0, 0), [new VoucherHistoryRow(1001, "Ірина Шевчук", -350m, new DateTime(2026, 10, 1, 10, 0, 0))]);

    public VoucherPagesTests()
    {
        Services.AddScoped<MarketingApi>();
        Api.Respond("/marketing/vouchers", HttpStatusCode.OK, new List<VoucherCardDto> { Gift });
        Api.Respond("/marketing/vouchers/themes", HttpStatusCode.OK, new List<LookupItem>());
        Api.Respond("/marketing/vouchers/1", HttpStatusCode.OK, Gift);
        Api.Respond("/marketing/vouchers/1", HttpStatusCode.OK, Gift, HttpMethod.Put);
        Api.Respond("/marketing/vouchers/1/sms", HttpStatusCode.OK, new SmsSendResult(false, "SMS-ключ не задано"), HttpMethod.Post);
    }

    [Fact]
    public void List_ShowsTheState_AndTheDialogSavesAndTexts()
    {
        var provider = Render<MudBlazor.MudDialogProvider>();
        var page = Render<Vouchers>();
        page.WaitForAssertion(() => Assert.Contains("GIFT1000", page.Find("[data-test=vouchers]").TextContent));
        Assert.Equal("діє", Vouchers.State(Gift).Name);

        page.Find("[data-test=vouchers] tbody tr td").Click();
        provider.WaitForAssertion(() => Assert.Contains("Ірина Шевчук", provider.Find("[data-test=history]").TextContent));
        Assert.True(provider.Find("button[data-test=delete]").HasAttribute("disabled")); // ним уже платили

        provider.Find("input[data-test=amount]").Change("1200");
        provider.Find("button[data-test=save]").Click();
        provider.WaitForAssertion(() => Assert.Contains("/marketing/vouchers/1", Api.Bodies.Keys));
        Assert.Contains("\"amount\":1200", Api.Bodies["/marketing/vouchers/1"]);
        Assert.Contains("\"code\":\"GIFT1000\"", Api.Bodies["/marketing/vouchers/1"]);

        page.Find("[data-test=vouchers] tbody tr td").Click();
        provider.WaitForElement("input[data-test=phone]").Change("0671112233");
        provider.Find("button[data-test=send-sms]").Click();
        provider.WaitForAssertion(() => Assert.Contains("\"phone\":\"0671112233\"", Api.Bodies["/marketing/vouchers/1/sms"]));
    }

    [Fact]
    public void OrderPanel_SpendsACode_WithTheWholeAvailableSumWhenNoAmountIsTyped()
    {
        Api.Respond("/orders/1001/voucher", HttpStatusCode.NoContent);
        Api.Respond("/orders/1001/voucher", HttpStatusCode.OK, new OrderVoucherDto(1001, 1, "GIFT1000", 650m, 850m, 0m), HttpMethod.Post);
        var reloads = 0;

        var panel = Render<VoucherPanel>(p => p.Add(c => c.OrderId, 1001).Add(c => c.OnChanged, () => reloads++));
        panel.WaitForElement("input[data-test=voucher-code]").Change("gift1000");
        panel.Find("button[data-test=voucher-redeem]").Click();

        panel.WaitForAssertion(() => Assert.Contains("GIFT1000", panel.Find("[data-test=voucher-spent]").TextContent));
        Assert.Equal("{\"code\":\"gift1000\",\"amount\":null}", Api.Bodies["/orders/1001/voucher"]);
        Assert.Equal(1, reloads);
    }

    [Fact]
    public void OrderPanel_ShowsTheSpentVoucherInsteadOfTheForm()
    {
        Api.Respond("/orders/1002/voucher", HttpStatusCode.OK, new OrderVoucherDto(1002, 1, "GIFT1000", 300m, 500m, 350m));

        var panel = Render<VoucherPanel>(p => p.Add(c => c.OrderId, 1002));

        panel.WaitForElement("button[data-test=voucher-release]");
        Assert.Empty(panel.FindAll("input[data-test=voucher-code]"));
    }
}
