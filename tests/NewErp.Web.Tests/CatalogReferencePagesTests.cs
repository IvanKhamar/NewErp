using System.Net;
using System.Text.Json.Nodes;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Catalog;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

/// <summary>Сторінка довідників (пункт 49) з підміненим API: групи зі значеннями, виробники, завантаження.</summary>
public class CatalogReferencePagesTests : WebTestContext
{
    private static readonly ReferenceGroupDto Colour = new(4, "Колір", 0, "select", [new ReferenceItemDto(40, "Червоний", 0, "", 2)]);

    public CatalogReferencePagesTests()
    {
        Services.AddScoped<CatalogApi>();
        Api.Respond("/catalog/references/filters", HttpStatusCode.OK, new List<ReferenceGroupDto> { Colour });
        Api.Respond("/catalog/references/attributes", HttpStatusCode.OK, new List<ReferenceGroupDto>());
        Api.Respond("/catalog/references/options", HttpStatusCode.OK, new List<ReferenceGroupDto>());
        Api.Respond("/catalog/references/filters/4/values", HttpStatusCode.Created, Colour with { Items = [.. Colour.Items, new ReferenceItemDto(41, "Синій")] }, HttpMethod.Post);
        Api.Respond("/catalog/manufacturers", HttpStatusCode.OK, new List<ManufacturerDto> { new(3, "Vidro", "", 0, 5) });
        Api.Respond("/catalog/downloads", HttpStatusCode.OK, new List<DownloadDto> { new(1, "Догляд", "care.pdf", "care.pdf", 0) });
    }

    [Fact]
    public void AGroupShowsItsValues_AndANewValueIsSentToTheGroup()
    {
        var page = Render<References>();
        page.WaitForAssertion(() => Assert.Contains("Колір", page.Find("[data-test=filters-groups]").TextContent));

        page.Find("[data-test=filters-groups] tbody tr").Click();
        Assert.Contains("Червоний", page.Find("[data-test=filters-values]").TextContent);
        Assert.True(page.Find("[data-test=filters-values] button").HasAttribute("disabled")); // значення є в 2 товарах

        page.Find("input[data-test=filters-value-name]").Change("Синій");
        page.Find("button[data-test=filters-value-save]").Click();
        page.WaitForAssertion(() => Assert.Contains("/catalog/references/filters/4/values", Api.Bodies.Keys));
        Assert.Equal("Синій", JsonNode.Parse(Api.Bodies["/catalog/references/filters/4/values"])!["name"]!.GetValue<string>());
    }
}
