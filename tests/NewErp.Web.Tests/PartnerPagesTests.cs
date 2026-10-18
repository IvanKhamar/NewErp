using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Partners;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

public class PartnerPagesTests : WebTestContext
{
    public PartnerPagesTests()
    {
        Services.AddScoped<PartnersApi>();
        Api.Respond("/partners/categories", HttpStatusCode.OK, new List<ShopCategoryDto> { new(5, "Сорочки", ["M", "S"]) });
    }

    [Fact]
    public void Card_ShowsMappingsAndFeedLink_AndSavesThem()
    {
        Api.Respond("/partners/3", HttpStatusCode.OK, new PartnerDto(3, "eva", "EVA", true, "", true, "Vidro", "", 5, DateTime.Now,
            [new PartnerCategoryMapDto(41, 5, "Сорочки", "M", "77", "", 10, null)],
            [new PartnerProductMapDto(42, 12, "Сукня", false, null, "EVA-12", "", 1300, null)]));
        Api.Respond("/partners/3", HttpStatusCode.NoContent, method: HttpMethod.Put);
        var page = Render<PartnerCard>(p => p.Add(c => c.Id, 3));

        page.WaitForAssertion(() => Assert.Contains("http://localhost/feeds/eva.xml", page.Find("[data-test=feed-link]").TextContent));
        Assert.Contains("41", page.Find("[data-test=category-map]").TextContent);
        Assert.Contains("вимкнено, у фід не йде", page.Find("[data-test=product-map]").TextContent);

        page.Find("[data-test=save] button, button[data-test=save]").Click();

        page.WaitForAssertion(() => Assert.Contains("/partners/3", Api.Bodies.Keys));
        var body = Api.Bodies["/partners/3"];
        Assert.Contains("\"categoryId\":5", body);
        Assert.Contains("\"sizeName\":\"M\"", body);
        Assert.Contains("\"externalId\":\"EVA-12\"", body);
    }

    [Fact]
    public void List_ShowsFeedState()
    {
        Api.Respond("/partners", HttpStatusCode.OK, new List<PartnerListItemDto> { new(3, "eva", "EVA", "Vidro", true, true, 2, 1) });

        var page = Render<Partners>();

        page.WaitForAssertion(() => Assert.Contains("увімкнено", page.Markup));
        Assert.Contains("eva", page.Markup);
    }
}
