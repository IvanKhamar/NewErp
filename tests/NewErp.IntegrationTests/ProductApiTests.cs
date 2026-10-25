using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>
/// Товари магазину, перенесено з Java ProductControllerTest. До товару ApiFactory (№102, «Сорочка лляна оверсайз»)
/// додаємо вигадані: 70 і 72 — сукні, 71 вимкнений.
/// </summary>
public class ProductApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task ListFiltersPages_CardAndLookups()
    {
        await SeedAsync(factory);
        var manager = await factory.LoginAsync(ApiFactory.Manager);

        async Task<int[]> Ids(string query) =>
            (await manager.GetFromJsonAsync<PagedResult<ProductListItem>>("/catalog/products" + query))!.Items.Select(p => p.Id).ToArray();

        var all = (await manager.GetFromJsonAsync<PagedResult<ProductListItem>>("/catalog/products"))!;
        Assert.Equal(4, all.TotalCount);
        Assert.Equal([71, ApiFactory.CategoryProductId, 70, 72], all.Items.Select(p => p.Id)); // за назвою
        Assert.Equal(new ProductListItem(70, "Сукня лляна", "SL-01", 1500m, true, 6, null), all.Items.Single(p => p.Id == 70));
        Assert.Equal(new[] { 70, 72 }, await Ids("?categoryId=30&enabled=true"));
        Assert.Equal(new[] { 70 }, await Ids("?labelId=1"));
        Assert.Equal(new[] { 71 }, await Ids("?enabled=false"));
        // % у пошуку — звичайний символ, а не шаблон LIKE; число знаходить сам товар.
        Assert.Equal(new[] { 72 }, await Ids("?search=" + Uri.EscapeDataString("50%")));
        Assert.Equal(new[] { 71 }, await Ids("?search=71"));
        var second = (await manager.GetFromJsonAsync<PagedResult<ProductListItem>>("/catalog/products?page=2&pageSize=2"))!;
        Assert.Equal((2, 2, [70, 72]), (second.Page, second.TotalPages, second.Items.Select(p => p.Id).ToArray()));

        var card = (await manager.GetFromJsonAsync<ProductCardDto>("/catalog/products/70"))!;
        Assert.Equal(("Сукня лляна", "SL-01", new DateTime(2026, 10, 1, 10, 0, 0)), (card.Name, card.Model, card.DateModified));
        Assert.Equal([new ProductSize(902, "S", 4), new ProductSize(903, "L", 2)], card.Sizes);
        Assert.Equal([new LookupItem(1, "Хіт"), new LookupItem(2, "Новинка")], card.Labels);
        Assert.Equal([new LookupItem(30, "Сукні")], card.Categories);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync("/catalog/products/999")).StatusCode);

        Assert.Equal(["Хіт", "Новинка"], (await manager.GetFromJsonAsync<List<LookupItem>>("/catalog/products/labels"))!.Select(l => l.Name));
        // Вимкнену категорію не пропонуємо.
        Assert.Equal(["Аксесуари", "Сукні"], (await manager.GetFromJsonAsync<List<LookupItem>>("/catalog/products/categories"))!.Select(c => c.Name));

        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/catalog/products")).StatusCode);
    }

    internal static async Task SeedAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var dress = ApiFactory.Product(70, "SL-01", 1500m);
        dress.Quantity = 6;
        dress.DateModified = new DateTime(2026, 10, 1, 10, 0, 0);
        var belt = ApiFactory.Product(71, "P-02", 500m);
        belt.Status = false;
        db.DdProducts.AddRange(dress, belt, ApiFactory.Product(72, "SL-50%", 900m));
        db.DdProductDescriptions.AddRange(Description(70, "Сукня лляна"), Description(71, "Пояс шкіряний"), Description(72, "Сукня шовкова"));
        db.DdOptionValueDescriptions.AddRange(
            new DdOptionValueDescription { OptionValueId = 902, LanguageId = ApiFactory.LanguageId, OptionId = 11, Name = "S" },
            new DdOptionValueDescription { OptionValueId = 903, LanguageId = ApiFactory.LanguageId, OptionId = 11, Name = "L" });
        db.DdProductOptionValues.AddRange(Size(701, 70, 902, 4), Size(702, 70, 903, 2));
        db.DdCategories.AddRange(Category(30, true), Category(31, true), Category(32, false));
        db.DdCategoryDescriptions.AddRange(CategoryName(30, "Сукні"), CategoryName(31, "Аксесуари"), CategoryName(32, "Архів"));
        db.DdProductToCategories.AddRange(
            new DdProductToCategory { ProductId = 70, CategoryId = 30 }, new DdProductToCategory { ProductId = 72, CategoryId = 30 },
            new DdProductToCategory { ProductId = 71, CategoryId = 31 });
        db.DdProductLabels.AddRange(
            new DdProductLabel { LabelId = 1, Name = "Хіт", BackgroundColor = "", TextColor = "", SortOrder = 1, Status = true },
            new DdProductLabel { LabelId = 2, Name = "Новинка", BackgroundColor = "", TextColor = "", SortOrder = 2, Status = true });
        db.DdProductToLabels.AddRange(new DdProductToLabel { ProductId = 70, LabelId = 2 }, new DdProductToLabel { ProductId = 70, LabelId = 1 });
        await db.SaveChangesAsync();
    }

    private static DdProductDescription Description(int id, string name) => new()
    {
        ProductId = id, LanguageId = ApiFactory.LanguageId, Name = name, Description = "", Tag = "", MetaTitle = "", MetaDescription = "", MetaKeyword = "",
    };

    private static DdProductOptionValue Size(int id, int productId, int optionValueId, int quantity) => new()
    {
        ProductOptionValueId = id, ProductOptionId = 7, ProductId = productId, OptionId = 11, OptionValueId = optionValueId, Quantity = quantity,
        PricePrefix = "+", PointsPrefix = "+", WeightPrefix = "+",
    };

    private static DdCategory Category(int id, bool enabled) => new()
    {
        CategoryId = id, Status = enabled, DateAdded = new DateTime(2026, 1, 1), DateModified = new DateTime(2026, 1, 1),
    };

    private static DdCategoryDescription CategoryName(int id, string name) => new()
    {
        CategoryId = id, LanguageId = ApiFactory.LanguageId, Name = name, Description = "", MetaTitle = "", MetaDescription = "", MetaKeyword = "",
    };
}
