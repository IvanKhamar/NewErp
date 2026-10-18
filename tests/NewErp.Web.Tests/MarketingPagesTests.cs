using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Marketing;
using NewErp.Web.Api;
using NewErp.Web.Pages.Marketing;

namespace NewErp.Web.Tests;

public class MarketingPagesTests : WebTestContext
{
    public MarketingPagesTests()
    {
        Services.AddScoped<MarketingApi>();
        Api.Respond("/marketing/birthdays", HttpStatusCode.OK, new List<BirthdayContactDto>
        {
            new("380671112233", "Ольга", "Нова", "", "Львів", "@olga", 18, 10, new DateOnly(2026, 10, 18), 1),
        });
        Api.Respond("/marketing/customers/orders", HttpStatusCode.OK, new List<CustomerOrderDto>
        {
            new(15, new DateTime(2026, 5, 1), 1200m, "Накладений платіж", "5) Відправлено", "Сукня x1"),
        });
        Api.Respond("/marketing/birthdays/coupon-code", HttpStatusCode.OK, new CouponCodeDto("DNAB12CD34"), HttpMethod.Post);
        Api.Respond("/marketing/birthdays/sms", HttpStatusCode.OK, new BirthdaySmsResult(false, 7, "НЕ НАДІСЛАНО"), HttpMethod.Post);
    }

    [Fact]
    public void Birthdays_GreetWithCoupon()
    {
        var page = Render<Birthdays>();
        page.WaitForAssertion(() => Assert.Contains("Ольга Нова", page.Find("[data-test=contacts]").TextContent));
        Assert.Contains("18.10", page.Find("[data-test=contacts]").TextContent);

        page.Find("[data-test=contacts] tbody tr td:nth-child(2)").Click();
        page.WaitForAssertion(() => Assert.Contains("Сукня x1", page.Find("[data-test=orders]").TextContent));

        page.Find("button[data-test=coupon]").Click();
        page.WaitForAssertion(() => Assert.Contains("DNAB12CD34", page.Find("[data-test=greeting] textarea").GetAttribute("value") ?? page.Find("[data-test=greeting] textarea").TextContent));
        page.Find("button[data-test=send]").Click();

        page.WaitForAssertion(() => Assert.Contains("/marketing/birthdays/sms", Api.Bodies.Keys));
        var body = Api.Bodies["/marketing/birthdays/sms"];
        Assert.Contains("\"couponCode\":\"DNAB12CD34\"", body);
        Assert.Contains("\"phone\":\"380671112233\"", body);
    }

    [Fact]
    public void Export_LinkCarriesFieldAndSources()
    {
        var page = Render<ContactExport>();

        Assert.Equal("/marketing/contacts.csv?field=Phone&sources=Orders&sources=Questions", page.Find("a[data-test=download]").GetAttribute("href"));
    }
}
