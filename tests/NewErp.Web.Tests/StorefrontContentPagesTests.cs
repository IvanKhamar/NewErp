using System.Net;
using System.Text.Json.Nodes;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Catalog;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

/// <summary>Вміст вітрини (пункт 50) з підміненим API: сторінки, FAQ, блог, блоки головної.</summary>
public class StorefrontContentPagesTests : WebTestContext
{
    public StorefrontContentPagesTests()
    {
        Services.AddScoped<CatalogApi>();
        Api.Respond("/catalog/information", HttpStatusCode.OK, new List<InformationDto> { new(1, "Доставка", SeoKeyword: "dostavka", Bottom: true) });
        Api.Respond("/catalog/information", HttpStatusCode.Created, new InformationDto(2, "Оплата"), HttpMethod.Post);
        Api.Respond("/catalog/faq", HttpStatusCode.OK, new List<FaqDto> { new(1, "Як прати льон?") });
        Api.Respond("/catalog/blog/categories", HttpStatusCode.OK, new List<BlogCategoryDto> { new(3, "Новини", true, 1) });
        Api.Respond("/catalog/blog/posts", HttpStatusCode.OK, new List<BlogPostDto>
        {
            new(5, "Нова колекція", DateAvailable: new DateTime(2026, 11, 1, 10, 0, 0), CategoryIds: [3]),
        });
        Api.Respond("/catalog/blocks/about", HttpStatusCode.OK, new AboutBlockDto(true, "Про нас", Items: [new AboutItemDto("catalog/a.jpg")]));
        Api.Respond("/catalog/blocks/nft", HttpStatusCode.OK, new NftBlockDto(HeadingTitle: "NFT", Link: "/nft"));
        Api.Respond("/catalog/blocks/nft", HttpStatusCode.OK, new NftBlockDto(HeadingTitle: "NFT", Link: "/nft"), HttpMethod.Put);
    }

    [Fact]
    public void Pages_ListTheirFooterLinks_AndANewPageIsSaved()
    {
        var page = Render<ContentPages>();
        page.WaitForAssertion(() => Assert.Contains("у підвалі", page.Find("[data-test=information]").TextContent));
        Assert.Contains("Як прати льон?", page.Find("[data-test=faq]").TextContent);

        page.Find("input[data-test=information-title]").Change("Оплата");
        page.Find("button[data-test=information-save]").Click();
        page.WaitForAssertion(() => Assert.Contains("/catalog/information", Api.Bodies.Keys));
        Assert.Equal("Оплата", JsonNode.Parse(Api.Bodies["/catalog/information"])!["title"]!.GetValue<string>());
    }

    [Fact]
    public void Blog_ShowsPostsWithTheirRubrics()
    {
        var page = Render<ContentBlog>();
        page.WaitForAssertion(() => Assert.Contains("Нова колекція", page.Find("[data-test=blog-posts]").TextContent));
        Assert.Contains("Новини", page.Find("[data-test=blog-posts]").TextContent);
        Assert.Contains("01.11.2026 10:00", page.Find("[data-test=blog-posts]").TextContent);
    }

    [Fact]
    public void Blocks_LoadAndSaveTheNftBlock()
    {
        var page = Render<ContentBlocks>();
        page.WaitForAssertion(() => Assert.Equal("Про нас", page.Find("input[data-test=about-title]").GetAttribute("value")));
        Assert.Equal("/nft", page.Find("input[data-test=nft-link]").GetAttribute("value"));

        page.Find("button[data-test=nft-save]").Click();
        page.WaitForAssertion(() => Assert.Contains("/catalog/blocks/nft", Api.Bodies.Keys));
        Assert.Equal("/nft", JsonNode.Parse(Api.Bodies["/catalog/blocks/nft"])!["link"]!.GetValue<string>());
    }
}
