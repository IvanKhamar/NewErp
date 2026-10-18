using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Partners;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>Партнери й XML-фід. Окрема тестова база, бо тест додає фото й назву категорії в каталог.</summary>
public class PartnerApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Partner_FeedListsCategoryProductsWithMarkup()
    {
        await AddCatalogDataAsync();
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        var request = new SavePartnerRequest("eva", "EVA", FeedEnabled: true, DefaultQuantity: 3,
            CategoryMap: [new CategoryMapRow(ApiFactory.CategoryId, ExternalId: "77", PercentSupply: 10, PercentSale: 20)]);
        var created = await manager.PostAsJsonAsync("/partners", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<Created>())!.Id;

        var partner = (await manager.GetFromJsonAsync<PartnerDto>($"/partners/{id}"))!;
        var categoryRow = Assert.Single(partner.CategoryMap);
        Assert.Equal("Сорочки", categoryRow.CategoryName);
        var categories = (await manager.GetFromJsonAsync<List<ShopCategoryDto>>("/partners/categories"))!;
        Assert.Equal(["M"], categories.Single(c => c.Id == ApiFactory.CategoryId).Sizes);

        // Фід відкривається без входу, як його забирає сайт партнера.
        var feed = XDocument.Parse(await factory.CreateClient().GetStringAsync("/feeds/eva.xml"));
        var offer = Assert.Single(feed.Descendants("offer"));
        Assert.Equal($"{ApiFactory.CategoryProductId}_{ApiFactory.SizeMOptionValueId}", offer.Attribute("id")!.Value);
        Assert.Equal(("990", "1080", "3"), (offer.Element("price")!.Value, offer.Element("price_old")!.Value, offer.Element("stock_quantity")!.Value));
        Assert.Equal(categoryRow.Id.ToString(), offer.Element("categoryId")!.Value);
        Assert.Equal(["image/catalog/main.jpg", "image/catalog/%D0%B7%D0%B0%D0%B4.jpg"], offer.Elements("picture").Select(p => p.Value));
        Assert.Equal("Сорочка лляна оверсайз - M", offer.Element("name_ua")!.Value);

        // Зміна відповідності не змінює id категорії у фіді; вимкнений фід — 404.
        (await manager.PutAsJsonAsync($"/partners/{id}", request with { FeedEnabled = false, CategoryMap = [request.CategoryMap![0] with { PercentSupply = 15 }] }))
            .EnsureSuccessStatusCode();
        Assert.Equal(categoryRow.Id, (await manager.GetFromJsonAsync<PartnerDto>($"/partners/{id}"))!.CategoryMap.Single().Id);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/feeds/eva.xml")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync("/partners", request)).StatusCode); // код уже зайнятий
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/partners")).StatusCode);

        (await manager.DeleteAsync($"/partners/{id}")).EnsureSuccessStatusCode();
        Assert.Empty((await manager.GetFromJsonAsync<List<PartnerListItemDto>>("/partners"))!);
    }

    // Ціна розміру M, головне й додаткове фото товару, назва категорії — те, чого немає в спільних тестових даних.
    private async Task AddCatalogDataAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.DdProductOptionValues.Where(v => v.ProductId == ApiFactory.CategoryProductId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Price, 900m));
        await db.DdProducts.Where(p => p.ProductId == ApiFactory.CategoryProductId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Image, "catalog/main.jpg"));
        db.DdProductImages.Add(new DdProductImage { ProductId = ApiFactory.CategoryProductId, Image = "catalog/зад.jpg", SortOrder = 1 });
        db.DdCategoryDescriptions.Add(new DdCategoryDescription
        {
            CategoryId = ApiFactory.CategoryId, LanguageId = ApiFactory.LanguageId, Name = "Сорочки",
            Description = "", MetaTitle = "", MetaDescription = "", MetaKeyword = "",
        });
        await db.SaveChangesAsync();
    }
}
