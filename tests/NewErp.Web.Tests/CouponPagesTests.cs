using System.Net;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Common;
using NewErp.Application.Marketing;
using NewErp.Web.Api;
using NewErp.Web.Pages.Marketing;

namespace NewErp.Web.Tests;

/// <summary>Сторінка й діалог купонів з підміненим API.</summary>
public class CouponPagesTests : WebTestContext
{
    private static readonly CouponListItem Listed = new(1, "Осінь", "AUTUMN10", "P", 10, 1000, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), 0, true, 1);

    private static readonly CouponCardDto Card = new(
        1, "Осінь", "AUTUMN10", "P", 10, 1000, false, false, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), 0, 1, true, new DateTime(2026, 10, 1),
        [new LookupItem(102, "Сорочка")], [new LookupItem(5, "Сорочки")], [], [],
        [new CouponHistoryRow(1001, 0, -150, new DateTime(2026, 11, 2))]);

    public CouponPagesTests()
    {
        Services.AddScoped<MarketingApi>();
        Services.AddScoped<ProductsApi>();
        Api.Respond("/marketing/coupons", HttpStatusCode.OK, new List<CouponListItem> { Listed });
        Api.Respond("/marketing/coupons/1", HttpStatusCode.OK, Card);
        Api.Respond("/marketing/coupons/1", HttpStatusCode.OK, Card, HttpMethod.Put);
        Api.Respond("/marketing/coupons", HttpStatusCode.OK, Card, HttpMethod.Post);
        Api.Respond("/catalog/products/categories", HttpStatusCode.OK, new List<LookupItem> { new(5, "Сорочки"), new(6, "Сукні") });
    }

    [Fact]
    public void List_ShowsUse_AndTheDialogKeepsTheListsWhenSaving()
    {
        var provider = Render<MudBlazor.MudDialogProvider>();
        var page = Render<Coupons>();
        page.WaitForAssertion(() => Assert.Contains("AUTUMN10", page.Find("[data-test=coupons]").TextContent));
        Assert.Contains("10 %", page.Find("[data-test=coupons]").TextContent);

        page.Find("[data-test=coupons] tbody tr td").Click();
        provider.WaitForAssertion(() => Assert.Contains("№1001", provider.Find("[data-test=history]").TextContent));
        Assert.True(provider.Find("button[data-test=delete]").HasAttribute("disabled")); // ним уже платили
        Assert.Equal("102", provider.Find("input[data-test=product-ids]").GetAttribute("value"));

        provider.Find("button[data-test=save]").Click();
        provider.WaitForAssertion(() => Assert.Contains("/marketing/coupons/1", Api.Bodies.Keys));
        var body = Api.Bodies["/marketing/coupons/1"];
        Assert.Contains("\"productIds\":[102]", body);
        Assert.Contains("\"categoryIds\":[5]", body);
        Assert.Contains("\"dateEnd\":\"2026-11-30\"", body);
    }

    [Fact]
    public void NewCoupon_RefusesAProductThatIsNotANumber_BeforeAskingTheServer()
    {
        var provider = Render<MudBlazor.MudDialogProvider>();
        var page = Render<Coupons>();
        page.WaitForElement("button[data-test=new-coupon]").Click();
        provider.WaitForElement("input[data-test=product-ids]").Change("101, сто");

        provider.Find("button[data-test=save]").Click();

        provider.WaitForAssertion(() => Assert.Contains("«сто» — не номер товару.", provider.Find("[data-test=error]").TextContent));
        Assert.DoesNotContain("/marketing/coupons", Api.Bodies.Keys);
        Assert.True(provider.Find("input[data-test=name]").GetAttribute("value") is "" or null); // форма лишається
    }
}
