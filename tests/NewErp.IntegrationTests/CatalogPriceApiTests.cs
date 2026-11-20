using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;

using NewErp.Application.Catalog;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>
/// Масові ціни (пункт 46) з переглядом перед записом і фільтри розмірів (пункт 52); лише адміністратор.
/// Перенесено з Python tests/test_catalog_prices.py і tests/test_catalog_maintenance.py.
/// </summary>
public class CatalogPriceApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int DressesId = 6;
    private const int OtherCategoryId = 7;
    private const int SizeL = 48;

    private static readonly string[] Models = ["PR-1", "PR-2", "PR-3"];

    /// <summary>Три товари двох категорій; база спільна для тестів класу, тож створюються лише раз.</summary>
    private async Task<List<int>> ProductsAsync()
    {
        var existing = await ProductFormApiTests.QueryAsync(factory, db =>
            db.DdProducts.Where(p => Models.Contains(p.Model)).OrderBy(p => p.Model).Select(p => p.ProductId).ToListAsync());
        if (existing.Count == Models.Length)
            return existing;
        await ProductFormApiTests.SeedAsync(factory);
        await ProductFormApiTests.WithDbAsync(factory, async db =>
        {
            if (await db.DdCategories.AnyAsync(c => c.CategoryId == DressesId))
                return;
            foreach (var (id, name) in new[] { (DressesId, "Сукні"), (OtherCategoryId, "Сорочки") })
            {
                db.DdCategories.Add(new DdCategory
                {
                    CategoryId = id, Top = false, Column = 0, SortOrder = 0, Status = true,
                    DateAdded = new DateTime(2026, 1, 1), DateModified = new DateTime(2026, 1, 1),
                });
                db.DdCategoryDescriptions.Add(new DdCategoryDescription
                {
                    CategoryId = id, LanguageId = ApiFactory.LanguageId, Name = name,
                    Description = "", MetaTitle = "", MetaDescription = "", MetaKeyword = "",
                });
            }
            await db.SaveChangesAsync();
        });
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        var ids = new List<int>();
        foreach (var (model, price, category) in new[] { ("PR-1", 1000, DressesId), ("PR-2", 1490, DressesId), ("PR-3", 700, OtherCategoryId) })
            ids.Add(await ProductFormApiTests.CreateAsync(manager, new JsonObject
            {
                ["name"] = $"Товар {model}", ["model"] = model, ["price"] = price, ["categoryIds"] = new JsonArray(category),
            }));
        return ids;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [MySqlFact]
    public async Task PriceChange_PreviewsThenAppliesOnlyTheChosenCategory()
    {
        var ids = await ProductsAsync();
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        var body = new PriceChangeRequest("price", "add", 10, "percent", CategoryIds: [DressesId]);

        var preview = await PostAsync<PriceChangePreview>(admin, "/catalog/prices/preview", body);
        Assert.Equal((2, 2), (preview.Total, preview.Changed));
        Assert.Equal([(1000m, 1100m), (1490m, 1639m)], preview.Rows.Select(r => (r.Old!.Value, r.New!.Value))); // цілі гривні
        Assert.Equal(1000m, (await admin.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{ids[0]}/edit"))!.Price); // ще нічого не записано

        Assert.Equal(new PriceChangeResult(2), await PostAsync<PriceChangeResult>(admin, "/catalog/prices/apply", body));
        Assert.Equal(1639m, (await admin.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{ids[1]}/edit"))!.Price);
        Assert.Equal(700m, (await admin.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{ids[2]}/edit"))!.Price); // інша категорія
    }

    [MySqlFact]
    public async Task SalePercent_CreatesOrUpdatesSales_AndRemoveTakesThemOff()
    {
        var ids = await ProductsAsync();
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        var body = new PriceChangeRequest("sale", "set_percent", 20, CategoryIds: [OtherCategoryId]);

        Assert.Equal(new PriceChangeResult(1), await PostAsync<PriceChangeResult>(admin, "/catalog/prices/apply", body));
        var special = Assert.Single((await admin.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{ids[2]}/edit"))!.Specials);
        Assert.Equal((20m, 0m, (DateOnly?)null), (special.Percent, special.Price, special.DateEnd));
        Assert.Equal(new PriceChangeResult(0), await PostAsync<PriceChangeResult>(admin, "/catalog/prices/apply", body)); // те саме вдруге нічого не змінює

        var remove = new PriceChangeRequest("sale", "remove", CategoryIds: [OtherCategoryId]);
        Assert.Equal(new PriceChangeResult(1), await PostAsync<PriceChangeResult>(admin, "/catalog/prices/apply", remove));
        Assert.Empty((await admin.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{ids[2]}/edit"))!.Specials);
    }

    public static TheoryData<string> UnclearRequests => new()
    {
        """{"target": "price", "operation": "add", "amount": 10}""", // ні категорії, ні «усі товари»
        """{"target": "price", "operation": "set_percent", "amount": 10, "allProducts": true}""",
        """{"target": "sale", "operation": "set_percent", "amount": 100, "allProducts": true}""",
        """{"target": "price", "operation": "divide", "amount": 0, "allProducts": true}""",
    };

    [MySqlTheory]
    [MemberData(nameof(UnclearRequests))]
    public async Task PriceChange_RefusesUnclearRequests(string body)
    {
        await ProductsAsync();
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        var response = await admin.PostAsync("/catalog/prices/preview", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [MySqlFact]
    public async Task BulkChanges_AreForTheAdminOnly()
    {
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync("/catalog/prices/preview", new PriceChangeRequest(AllProducts: true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync("/catalog/prices/size-filters", null)).StatusCode);
    }

    [MySqlFact]
    public async Task SizeFilters_AreAddedRemovedAndCreated()
    {
        await ProductFormApiTests.SeedAsync(factory); // розмір L
        await ProductFormApiTests.WithDbAsync(factory, async db =>
        {
            db.DdFilters.AddRange(new DdFilter { FilterId = 300, FilterGroupId = 3 }, new DdFilter { FilterId = 301, FilterGroupId = 3 });
            db.DdFilterDescriptions.AddRange(
                new DdFilterDescription { FilterId = 300, LanguageId = ApiFactory.LanguageId, FilterGroupId = 3, Name = "M" },
                new DdFilterDescription { FilterId = 301, LanguageId = ApiFactory.LanguageId, FilterGroupId = 3, Name = "S" });
            db.DdProductFilters.Add(new DdProductFilter { ProductId = ApiFactory.CategoryProductId, FilterId = 301 }); // S, якого вже немає
            db.DdProductOptionValues.Add(new DdProductOptionValue // другий розмір товару — L
            {
                ProductOptionValueId = 503, ProductOptionId = 51, ProductId = ApiFactory.CategoryProductId, OptionId = 11, OptionValueId = SizeL,
                PricePrefix = "+", PointsPrefix = "+", WeightPrefix = "+",
            });
            await db.SaveChangesAsync();
        });
        var admin = await factory.LoginAsync(ApiFactory.Admin);

        // L нове; M і L цього товару та M товару 101 зв'язано; S знято.
        Assert.Equal(new SizeFilterResult(1, 3, 1), await PostAsync<SizeFilterResult>(admin, "/catalog/prices/size-filters", new { }));
        var names = await ProductFormApiTests.QueryAsync(factory, db => (
            from pf in db.DdProductFilters
            join d in db.DdFilterDescriptions on pf.FilterId equals d.FilterId
            where pf.ProductId == ApiFactory.CategoryProductId
            orderby d.Name
            select d.Name).ToListAsync());
        Assert.Equal(["L", "M"], names);
        Assert.Equal(new SizeFilterResult(0, 0, 0), await PostAsync<SizeFilterResult>(admin, "/catalog/prices/size-filters", new { }));
    }
}
