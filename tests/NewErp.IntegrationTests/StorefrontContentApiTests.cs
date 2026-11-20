using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;

using NewErp.Application.Catalog;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>Вміст вітрини (пункт 50): сторінки, FAQ, блог, блоки головної. Перенесено з Python tests/test_storefront_content.py.</summary>
public class StorefrontContentApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [MySqlFact]
    public async Task InformationPages_CleanHtml_KeepUniqueAddresses_AndDelete()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var created = await client.PostAsJsonAsync("/catalog/information", new InformationDto(
            Title: "Доставка", Description: "<h2>Умови</h2><iframe src=\"https://x\"></iframe><p style=\"x\">Текст</p>", SeoKeyword: "dostavka", Bottom: true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var page = (await created.Content.ReadFromJsonAsync<InformationDto>())!;
        Assert.Equal(("<h2>Умови</h2><p>Текст</p>", true, "dostavka"), (page.Description, page.Bottom, page.SeoKeyword));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/catalog/information", new InformationDto(Title: "Інша", SeoKeyword: "dostavka"))).StatusCode);
        // Своя адреса лишається дозволеною.
        Assert.Equal("dostavka", (await ReadAsync<InformationDto>(await client.PutAsJsonAsync($"/catalog/information/{page.Id}", page with { Title = "Доставка й оплата" }))).SeoKeyword);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/catalog/information", new InformationDto(Title: " "))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/catalog/content/information/{page.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/catalog/content/information/{page.Id}")).StatusCode);
        // Адреса знову вільна.
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/catalog/information", new InformationDto(Title: "Нова", SeoKeyword: "dostavka"))).StatusCode);
    }

    [MySqlFact]
    public async Task FaqAndBlog()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var faq = await ReadAsync<FaqDto>(await client.PostAsJsonAsync("/catalog/faq",
            new FaqDto(Question: "Як прати льон?", Answer: "<p>При 30°</p>", Author: "Олена", Email: "olena@example.com")));
        Assert.Equal(("Як прати льон?", "<p>При 30°</p>"), (faq.Question, faq.Answer));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/catalog/faq", new FaqDto(Question: "Х", Email: "not-an-email"))).StatusCode);

        var rubric = await ReadAsync<BlogCategoryDto>(await client.PostAsJsonAsync("/catalog/blog/categories", new BlogCategoryDto(Name: "Новини")));
        var created = await client.PostAsJsonAsync("/catalog/blog/posts", new BlogPostDto(
            Name: "Нова колекція", Description: "<p>Льон</p><script>x</script>", CategoryIds: [rubric.Id], Image: "catalog/blog/1.jpg", SeoKeyword: "nova-kolektsiia"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var post = (await created.Content.ReadFromJsonAsync<BlogPostDto>())!;
        Assert.Equal("<p>Льон</p>", post.Description);
        Assert.Equal([rubric.Id], post.CategoryIds);
        Assert.NotNull(post.DateAvailable); // порожньо — зараз
        Assert.Equal(1, (await client.GetFromJsonAsync<List<BlogCategoryDto>>("/catalog/blog/categories"))!.Single(c => c.Id == rubric.Id).Posts);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/catalog/blog/posts", new BlogPostDto(Name: "Х", CategoryIds: [9999]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/catalog/blog/posts", new BlogPostDto(Name: "Х", Image: "/etc/x.jpg"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/catalog/content/blog-posts/{post.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/catalog/content/blog-categories/{rubric.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync("/catalog/content/pages/1")).StatusCode); // не вид вмісту
    }

    [MySqlFact]
    public async Task HomePageBlocks_KeepOtherLanguages()
    {
        await ProductFormApiTests.WithDbAsync(factory, async db =>
        {
            db.DdSettings.Add(new DdSetting
            {
                StoreId = 0, Code = "module_about", Key = "module_about_description", Serialized = true,
                Value = """{"1": {"heading_title": "About us", "subtitle": "", "description": "<p>EN</p>"}}""",
            });
            await db.SaveChangesAsync();
        });
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var saved = await ReadAsync<AboutBlockDto>(await client.PutAsJsonAsync("/catalog/blocks/about", new AboutBlockDto(
            Enabled: true, HeadingTitle: "Про нас", Description: "<p>Шиємо</p><img src=x onerror=y>", Items: [new AboutItemDto(ImageLeft: "catalog/about/l.jpg", SortOrder: 1)])));
        Assert.Equal((true, "Про нас", "<p>Шиємо</p><img src=\"x\">"), (saved.Enabled, saved.HeadingTitle, saved.Description));
        Assert.Equal("catalog/about/l.jpg", saved.Items[0].ImageLeft);
        var stored = await ProductFormApiTests.QueryAsync(factory, db =>
            db.DdSettings.Where(s => s.Code == "module_about" && s.Key == "module_about_description").Select(s => s.Value).SingleAsync());
        Assert.Equal("About us", JsonNode.Parse(stored)!["1"]!["heading_title"]!.GetValue<string>()); // англійський текст лишився
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/catalog/blocks/about", new AboutBlockDto(Items: [new AboutItemDto(ImageLeft: "../x.jpg")]))).StatusCode);

        var nft = await ReadAsync<NftBlockDto>(await client.PutAsJsonAsync("/catalog/blocks/nft", new NftBlockDto(
            Enabled: true, HeadingTitle: "NFT", Link: "/nft", Images: [new NftImageDto("catalog/nft/1.png", "/a")], Items: [new NftItemDto("Крок 1", Description: "<b>Купіть</b>")])));
        Assert.Equal(("/nft", "/a", "Крок 1"), (nft.Link, nft.Images[0].Link, nft.Items[0].Title));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/catalog/blocks/nft", new NftBlockDto(Link: "javascript:alert(1)"))).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/catalog/blocks/nft")).StatusCode);
    }
}
