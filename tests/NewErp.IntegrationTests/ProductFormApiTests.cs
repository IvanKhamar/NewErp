using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Catalog;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>
/// Уся форма товару старої адмінки (пункти 43, 45): створити, змінити, копіювати, розміри, зв'язки й «зняти NEW».
/// Перенесено з Python tests/test_product_edit.py.
/// </summary>
public class ProductFormApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int SizeL = 48;
    private const int NewLabelId = 1;
    private const int HitLabelId = 2;
    private const int NewFilterId = 70;

    internal static JsonObject Form(Action<JsonObject>? change = null)
    {
        var form = new JsonObject
        {
            ["model"] = "DR-1",
            ["sku"] = "PRINT-1",
            ["price"] = 1200,
            ["name"] = "Сукня «Лев»",
            ["description"] = "<p onclick=\"steal()\">Лляна сукня<script>alert(1)</script></p><a href=\"javascript:x()\">x</a>",
            ["tableSize"] = "<table><tr><td>S</td></tr></table>",
            ["tag"] = "літо,тема:NEW",
            ["image"] = "catalog/dresses/lion.jpg",
            ["seoKeyword"] = "suknia-lev",
            ["categoryIds"] = new JsonArray(ApiFactory.CategoryId),
            ["labelIds"] = new JsonArray(NewLabelId),
            ["filterIds"] = new JsonArray(NewFilterId),
            ["images"] = new JsonArray(new JsonObject { ["image"] = "catalog/dresses/lion-2.jpg", ["sortOrder"] = 1 }),
            ["specials"] = new JsonArray(new JsonObject { ["customerGroupId"] = 1, ["priority"] = 1, ["price"] = 999 }),
            ["sizes"] = new JsonArray(
                new JsonObject { ["optionValueId"] = ApiFactory.SizeMOptionValueId, ["price"] = 0 },
                new JsonObject { ["optionValueId"] = SizeL, ["price"] = 1300 }),
        };
        change?.Invoke(form);
        return form;
    }

    [MySqlFact]
    public async Task Create_CleansHtml_AndSavesEveryPart()
    {
        var client = await ReadyAsync();
        var response = await client.PostAsJsonAsync("/catalog/products", Form());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var productId = (await response.Content.ReadFromJsonAsync<CreatedProductDto>())!.ProductId;
        Assert.Equal($"/catalog/products/{productId}", response.Headers.Location!.OriginalString);

        var product = (await client.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{productId}/edit"))!;
        Assert.Contains("Лляна сукня", product.Description);
        Assert.DoesNotContain("onclick", product.Description);
        Assert.DoesNotContain("script", product.Description);
        Assert.DoesNotContain("javascript", product.Description);
        Assert.Contains("rel=\"noopener noreferrer\"", product.Description);
        Assert.Contains("<tbody>", product.TableSize);
        Assert.Equal((false, "suknia-lev", "catalog/dresses/lion.jpg"), (product.Enabled, product.SeoKeyword, product.Image));
        Assert.Equal([ApiFactory.CategoryId], product.CategoryIds);
        Assert.Equal([NewLabelId], product.LabelIds);
        Assert.Equal([NewFilterId], product.FilterIds);
        Assert.Equal([(ApiFactory.SizeMOptionValueId, "M", 0m, 0), (SizeL, "L", 1300m, 0)],
            product.Sizes.Select(s => (s.OptionValueId, s.Name, s.Price, s.Quantity)));
        var special = Assert.Single(product.Specials);
        Assert.Equal((999m, (DateOnly?)null, (DateOnly?)null), (special.Price, special.DateStart, special.DateEnd)); // без меж

        // Акційна ціна працює в пошуку товару нового замовлення (той шукає лише увімкнені товари).
        var enabled = await client.PostAsJsonAsync("/catalog/products", Form(f => { f["model"] = "DR-SALE"; f["seoKeyword"] = ""; f["enabled"] = true; }));
        var enabledId = (await enabled.Content.ReadFromJsonAsync<CreatedProductDto>())!.ProductId;
        var found = Assert.Single((await client.GetFromJsonAsync<JsonElement>("/orders/new/products?search=DR-SALE")).EnumerateArray());
        Assert.Equal((enabledId, 999m, 1200m),
            (found.GetProperty("id").GetInt32(), found.GetProperty("price").GetDecimal(), found.GetProperty("regularPrice").GetDecimal()));
    }

    public static TheoryData<string, string> BadValues => new()
    {
        { "name", "\" \"" },
        { "image", "\"../../config.php\"" },
        { "image", "\"catalog/a.php\"" },
        { "seoKeyword", "\"Сукня лев\"" },
        { "categoryIds", "[999]" },
        { "sizes", "[{\"optionValueId\": 999}]" },
        { "relatedIds", "[999999]" },
        { "model", $"\"{new string('x', 65)}\"" },
    };

    [MySqlTheory]
    [MemberData(nameof(BadValues))]
    public async Task Create_RefusesBadValues(string field, string json)
    {
        var client = await ReadyAsync();
        var response = await client.PostAsJsonAsync("/catalog/products", Form(f => f[field] = JsonNode.Parse(json)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [MySqlFact]
    public async Task SeoAddress_MustBeUnique()
    {
        var client = await ReadyAsync();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/catalog/products", Form(f => f["seoKeyword"] = "unique-one"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/catalog/products", Form(f => f["seoKeyword"] = "unique-one"))).StatusCode);
    }

    [MySqlFact]
    public async Task Save_ChecksTheVersion_KeepsStock_AndLinksColoursBothWays()
    {
        var client = await ReadyAsync();
        var first = await CreateAsync(client, Form(f => f["seoKeyword"] = ""));
        var second = await CreateAsync(client, Form(f => { f["seoKeyword"] = ""; f["sizes"] = new JsonArray(); }));
        // Виробництво поклало залишок на розмір M; стара версія (OpenCart тримає цілі секунди), щоб розрізнити два збереження.
        await SqlAsync($"UPDATE dd_product_option_value SET quantity = 3 WHERE product_id = {first} AND option_value_id = {ApiFactory.SizeMOptionValueId}");
        await SqlAsync($"UPDATE dd_product SET date_modified = '2026-01-01' WHERE product_id = {first}");

        var product = await EditJsonAsync(client, first);
        var changed = product.DeepClone().AsObject();
        changed["price"] = 1500;
        changed["relatedColorIds"] = new JsonArray(second);
        changed["labelIds"] = new JsonArray(HitLabelId);
        changed["sizes"] = new JsonArray(new JsonObject { ["optionValueId"] = ApiFactory.SizeMOptionValueId, ["price"] = 1550 }); // L прибрано
        var saved = await client.PutAsJsonAsync($"/catalog/products/{first}", changed);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedProduct = (await saved.Content.ReadFromJsonAsync<ProductEditDto>())!;
        Assert.Equal([("M", 1550m, 3)], savedProduct.Sizes.Select(s => (s.Name, s.Price, s.Quantity)));
        Assert.Equal([first], (await client.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{second}/edit"))!.RelatedColorIds);

        // Форма, відкрита до збереження, тепер застаріла.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/catalog/products/{first}", changed)).StatusCode);
        var fresh = await EditJsonAsync(client, first);
        var noM = fresh.DeepClone().AsObject();
        noM["sizes"] = new JsonArray();
        var refused = await client.PutAsJsonAsync($"/catalog/products/{first}", noM);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("3 шт.", await refused.Content.ReadAsStringAsync());
        var noVersion = fresh.DeepClone().AsObject();
        noVersion["dateModified"] = null;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/catalog/products/{first}", noVersion)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/catalog/products/999999", fresh)).StatusCode);
    }

    [MySqlFact]
    public async Task Copy_IsDisabled_WithoutStockAndSeoAddress()
    {
        var client = await ReadyAsync();
        var source = await CreateAsync(client, Form(f => { f["seoKeyword"] = "to-copy"; f["enabled"] = true; }));
        await SqlAsync($"UPDATE dd_product_option_value SET quantity = 4 WHERE product_id = {source}");

        var copied = await client.PostAsync($"/catalog/products/{source}/copy", null);
        Assert.Equal(HttpStatusCode.Created, copied.StatusCode);
        var copyId = (await copied.Content.ReadFromJsonAsync<CreatedProductDto>())!.ProductId;
        var copy = (await client.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{copyId}/edit"))!;
        Assert.Equal(("Сукня «Лев»", false, ""), (copy.Name, copy.Enabled, copy.SeoKeyword));
        Assert.All(copy.Sizes, s => Assert.Equal(0, s.Quantity));
        Assert.Equal(999m, copy.Specials[0].Price);
    }

    [MySqlFact]
    public async Task RemoveNew_TakesTheLabelFilterAndTagOffAPrint()
    {
        var client = await ReadyAsync();
        var ids = new List<int>();
        for (var i = 0; i < 2; i++)
            ids.Add(await CreateAsync(client, Form(f => { f["sku"] = "PRINT-9"; f["seoKeyword"] = ""; })));

        var result = (await (await client.PostAsJsonAsync("/catalog/products/remove-new", new RemoveNewRequest("PRINT-9")))
            .Content.ReadFromJsonAsync<RemoveNewResult>())!;
        Assert.Equal(ids, result.ProductIds.Order());
        Assert.Equal((true, true), (result.LabelRemoved, result.FilterRemoved));
        var product = (await client.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{ids[0]}/edit"))!;
        Assert.Equal((0, 0, "літо"), (product.LabelIds.Count, product.FilterIds.Count, product.Tag));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/catalog/products/remove-new", new RemoveNewRequest("NOPE"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/catalog/products/remove-new", new RemoveNewRequest(" "))).StatusCode);
    }

    [MySqlFact]
    public async Task FormLookups_AndAccess()
    {
        var client = await ReadyAsync();
        var lookups = (await client.GetFromJsonAsync<ProductFormLookups>("/catalog/products/form-lookups"))!;
        Assert.Equal(["M", "L"], lookups.Sizes.Select(s => s.Name));
        Assert.Contains(lookups.Filters, f => f.Id == NewFilterId && f.Name == "7: NEW");
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.PostAsJsonAsync("/catalog/products", Form())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/catalog/products/{ApiFactory.CategoryProductId}/edit")).StatusCode);
    }

    [MySqlFact]
    public async Task ProductList_GivesTheVersion_ForTheQuickPriceEdit()
    {
        var client = await ReadyAsync();
        var id = await CreateAsync(client, Form(f => { f["model"] = "QUICK-1"; f["seoKeyword"] = ""; }));
        // Стара версія: OpenCart тримає цілі секунди, тож дві зміни в ту саму секунду були б однією версією.
        await SqlAsync($"UPDATE dd_product SET date_modified = '2026-01-01' WHERE product_id = {id}");
        var row = Assert.Single((await client.GetFromJsonAsync<JsonElement>("/catalog/products?search=QUICK-1")).GetProperty("items").EnumerateArray());
        var version = row.GetProperty("dateModified").GetDateTime();
        var quick = await client.PatchAsJsonAsync($"/catalog/products/{id}", new UpdateProductRequest(1111m, DateModified: version));
        Assert.Equal(HttpStatusCode.OK, quick.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PatchAsJsonAsync($"/catalog/products/{id}", new UpdateProductRequest(1222m, DateModified: version))).StatusCode);
    }

    internal static async Task<int> CreateAsync(HttpClient client, JsonObject form)
    {
        var response = await client.PostAsJsonAsync("/catalog/products", form);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CreatedProductDto>())!.ProductId;
    }

    internal static async Task<JsonObject> EditJsonAsync(HttpClient client, int productId) =>
        (await client.GetFromJsonAsync<JsonObject>($"/catalog/products/{productId}/edit"))!;

    private async Task<HttpClient> ReadyAsync()
    {
        await SeedAsync(factory);
        return await factory.LoginAsync(ApiFactory.Manager);
    }

    private Task SqlAsync(FormattableString sql) => WithDbAsync(factory, db => db.Database.ExecuteSqlInterpolatedAsync(sql));

    internal static async Task WithDbAsync(ApiFactory factory, Func<ErpDbContext, Task> work)
    {
        using var scope = factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    /// <summary>Категорія, мітки NEW і «Хіт», фільтр NEW групи «За темою» й розмір L (M є в загальному сіді).</summary>
    internal static Task SeedAsync(ApiFactory factory) => WithDbAsync(factory, async db =>
    {
        if (await db.DdProductLabels.AnyAsync(l => l.LabelId == NewLabelId))
            return;
        db.DdCategories.Add(new DdCategory
        {
            CategoryId = ApiFactory.CategoryId, Top = false, Column = 0, SortOrder = 0, Status = true,
            DateAdded = new DateTime(2026, 1, 1), DateModified = new DateTime(2026, 1, 1),
        });
        db.DdCategoryDescriptions.Add(new DdCategoryDescription
        {
            CategoryId = ApiFactory.CategoryId, LanguageId = ApiFactory.LanguageId, Name = "Сорочки",
            Description = "", MetaTitle = "", MetaDescription = "", MetaKeyword = "",
        });
        db.DdProductLabels.AddRange(
            new DdProductLabel { LabelId = NewLabelId, Name = "NEW", BackgroundColor = "", TextColor = "", SortOrder = 0, Status = true },
            new DdProductLabel { LabelId = HitLabelId, Name = "Хіт", BackgroundColor = "", TextColor = "", SortOrder = 1, Status = true });
        db.DdFilters.Add(new DdFilter { FilterId = NewFilterId, FilterGroupId = 7, SortOrder = 0 });
        db.DdFilterDescriptions.Add(new DdFilterDescription { FilterId = NewFilterId, LanguageId = ApiFactory.LanguageId, FilterGroupId = 7, Name = "NEW" });
        db.DdOptionValueDescriptions.Add(new DdOptionValueDescription { OptionValueId = SizeL, LanguageId = ApiFactory.LanguageId, OptionId = 11, Name = "L" });
        await db.SaveChangesAsync();
    });
}
