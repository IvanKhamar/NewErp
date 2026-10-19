using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Returns;
using NewErp.Web.Api;
using NewErp.Web.Pages.Returns;

namespace NewErp.Web.Tests;

public class ReturnPagesTests : WebTestContext
{
    private static readonly ReturnLookupsDto Lookups = new(
        [new(1, "Очікує"), new(2, "Отримано")], [new(1, "Не підійшов розмір")], [new(1, "Повернено кошти")]);

    [Fact]
    public void Card_ShowsDetailsAndHistory_AndSavesNewStatus()
    {
        Services.AddScoped<ReturnsApi>();
        Api.Respond("/returns/lookups", HttpStatusCode.OK, Lookups);
        Api.Respond("/returns/7", HttpStatusCode.OK, new ReturnDetails(
            7, 2045, 30, "Олена Коваль", "0501234567", "", "Сукня лляна", "SK-01", 1, false, 1, "Не підійшов розмір",
            0, null, 1, "Очікує", "Завеликий розмір", new DateTime(2026, 10, 18), new DateTime(2026, 10, 18),
            [new(new DateTime(2026, 10, 18), 1, "Очікує", "Завеликий розмір")]));
        Api.Respond("/returns/7/history", HttpStatusCode.NoContent, method: HttpMethod.Post);

        var page = Render<ReturnCard>(p => p.Add(c => c.Id, 7));

        page.WaitForAssertion(() => Assert.Contains("Сукня лляна", page.Find("[data-test=details]").TextContent));
        Assert.Equal("orders/2045", page.Find("[data-test=details] a").GetAttribute("href"));
        Assert.Contains("Очікує", page.Find("[data-test=status]").TextContent);

        page.Find("[data-test=add-history]").Click();

        page.WaitForAssertion(() => Assert.Contains("\"statusId\":1", Api.Bodies["/returns/7/history"]));
    }
}
