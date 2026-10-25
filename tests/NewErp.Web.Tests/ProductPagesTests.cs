using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

/// <summary>Сторінки товарів магазину з підробленим API; товари вигадані.</summary>
public class ProductPagesTests : WebTestContext
{
    [Fact]
    public void List_ShowsProductsAndAsksForTheFilters()
    {
        Services.AddScoped<ProductsApi>();
        Api.Respond("/catalog/products/categories", HttpStatusCode.OK, new List<LookupItem> { new(30, "Сукні") });
        Api.Respond("/catalog/products/labels", HttpStatusCode.OK, new List<LookupItem> { new(1, "Хіт") });
        Api.Respond("/catalog/products", HttpStatusCode.OK, new PagedResult<ProductListItem>(
            [new(70, "Сукня лляна", "SL-01", 1500m, true, 6, null), new(71, "Пояс шкіряний", "P-02", 500m, false, 0, null)], 1, 50, 2));

        var page = Render<Products>();

        page.WaitForAssertion(() => Assert.Contains("Сукня лляна", page.Find("[data-test=products]").TextContent));
        Assert.Contains("вимкнено", page.Find("[data-test=products]").TextContent);
        Assert.Contains("/catalog/products/categories", Api.Requests);
        Assert.Contains("/catalog/products/labels", Api.Requests);
    }

    [Fact]
    public void Card_ShowsSizesCategoriesAndLabels_OrNotFound()
    {
        Services.AddScoped<ProductsApi>();
        Api.Respond("/catalog/products/70", HttpStatusCode.OK, new ProductCardDto(70, "Сукня лляна", "SL-01", 1500m, true, 6, null,
            new DateTime(2026, 10, 1, 10, 0, 0), [new(902, "S", 4), new(903, "L", 2)], [new(1, "Хіт")], [new(30, "Сукні")]));
        Api.Respond("/catalog/products/labels", HttpStatusCode.OK, new List<LookupItem> { new(1, "Хіт") });
        Api.Respond("/catalog/products/categories", HttpStatusCode.OK, new List<LookupItem> { new(30, "Сукні") });

        var card = Render<ProductCard>(p => p.Add(c => c.Id, 70));
        card.WaitForAssertion(() => Assert.Equal(2, card.FindAll("[data-test=sizes] tbody tr").Count));
        Assert.Contains("Сукні", card.Find("[data-test=details]").TextContent);
        Assert.Contains("Хіт", card.Find("[data-test=details]").TextContent);

        var missing = Render<ProductCard>(p => p.Add(c => c.Id, 999));
        missing.WaitForAssertion(() => Assert.Contains("Товар №999 не знайдено.", missing.Find("[data-test=not-found]").TextContent));
        Assert.Equal("—", ProductCard.Names([]));
    }

    [Fact]
    public void Card_SaveSendsAPatchAndShowsTheNewCard()
    {
        Services.AddScoped<ProductsApi>();
        var opened = new ProductCardDto(70, "Сукня лляна", "SL-01", 1500m, true, 6, null, new DateTime(2026, 10, 1, 10, 0, 0), [], [], [new(30, "Сукні")]);
        Api.Respond("/catalog/products/70", HttpStatusCode.OK, opened);
        Api.Respond("/catalog/products/labels", HttpStatusCode.OK, new List<LookupItem> { new(1, "Хіт") });
        Api.Respond("/catalog/products/categories", HttpStatusCode.OK, new List<LookupItem> { new(30, "Сукні") });
        Api.Respond("/catalog/products/70", HttpStatusCode.OK, opened with { Price = 1399.5m, DateModified = new DateTime(2026, 10, 25, 12, 0, 0) },
            HttpMethod.Patch);

        var card = Render<ProductCard>(p => p.Add(c => c.Id, 70));
        card.WaitForAssertion(() => Assert.NotNull(card.Find("button[data-test=save]")));
        card.Find("button[data-test=save]").Click();

        card.WaitForAssertion(() => Assert.Contains("1 399,50", card.Markup.Replace(' ', ' ')));
        Assert.Equal(2, Api.Requests.Count(r => r == "/catalog/products/70")); // GET, потім PATCH
    }
}
