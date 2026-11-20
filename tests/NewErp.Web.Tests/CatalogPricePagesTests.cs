using System.Net;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

/// <summary>Сторінка масових цін (пункт 46) і фільтрів розмірів (пункт 52) з підміненим API.</summary>
public class CatalogPricePagesTests : WebTestContext
{
    public CatalogPricePagesTests()
    {
        Services.AddScoped<ProductsApi>();
        Api.Respond("/catalog/products/categories", HttpStatusCode.OK, new List<LookupItem> { new(6, "Сукні"), new(7, "Сорочки") });
        Api.Respond("/catalog/prices/preview", HttpStatusCode.OK,
            new PriceChangePreview(2, 1, [new PriceChangeRow(1, "Сукня лляна", "PR-1", 1000, 1100)]), HttpMethod.Post);
        Api.Respond("/catalog/prices/apply", HttpStatusCode.OK, new PriceChangeResult(1), HttpMethod.Post);
        Api.Respond("/catalog/prices/size-filters", HttpStatusCode.OK, new SizeFilterResult(1, 3, 1), HttpMethod.Post);
    }

    [Fact]
    public void Preview_ShowsTheChange_ThenApplySendsTheSameRequest()
    {
        var page = Render<Prices>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-test=category-6]")));
        Assert.True(page.Find("button[data-test=apply]").HasAttribute("disabled")); // спершу перегляд

        page.Find("input[data-test=category-6]").Change(true);
        page.Find("button[data-test=preview]").Click();
        page.WaitForAssertion(() => Assert.Contains("1100 ₴", page.Find("[data-test=rows]").TextContent));
        Assert.Contains("зміниться: 1", page.Find("[data-test=summary]").TextContent);
        Assert.Contains("\"categoryIds\":[6]", Api.Bodies["/catalog/prices/preview"]);

        page.Find("button[data-test=apply]").Click();
        page.WaitForAssertion(() => Assert.Contains("/catalog/prices/apply", Api.Bodies.Keys));
        Assert.Contains("\"target\":\"price\"", Api.Bodies["/catalog/prices/apply"]);
    }

    [Fact]
    public void SizeFilters_AreRebuiltByAButton()
    {
        var page = Render<Prices>();
        page.Find("button[data-test=size-filters]").Click();
        page.WaitForAssertion(() => Assert.Contains("/catalog/prices/size-filters", Api.Requests));
    }
}
