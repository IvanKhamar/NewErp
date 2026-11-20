using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;

using NewErp.Application.Catalog;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>Довідники каталогу (пункти 47, 48): мітки, пресети пошуку вітрини, категорії з деревом. Перенесено з Python tests/test_catalog_dictionaries.py.</summary>
public class CatalogDictionaryApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [MySqlFact]
    public async Task Labels_AreCreatedChecked_AndTakenOffProductsWhenDeleted()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var created = await client.PostAsJsonAsync("/catalog/labels", new LabelDto(Name: " Хіт ", BackgroundColor: "#ff0000", TextColor: "#FFFFFF"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var label = (await created.Content.ReadFromJsonAsync<LabelDto>())!;
        Assert.Equal(("Хіт", true, 0), (label.Name, label.Enabled, label.Products));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/catalog/labels", new LabelDto(Name: "X", BackgroundColor: "red;background:url(x)"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/catalog/labels", new LabelDto(Name: " "))).StatusCode);
        Assert.Equal(5, (await ReadAsync<LabelDto>(await client.PutAsJsonAsync($"/catalog/labels/{label.Id}", label with { SortOrder = 5 }))).SortOrder);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/catalog/labels/9999", label)).StatusCode);

        await ProductFormApiTests.WithDbAsync(factory, async db =>
        {
            db.DdProductToLabels.Add(new DdProductToLabel { ProductId = ApiFactory.CategoryProductId, LabelId = label.Id });
            await db.SaveChangesAsync();
        });
        Assert.Equal(1, (await client.GetFromJsonAsync<List<LabelDto>>("/catalog/labels"))!.Single(l => l.Id == label.Id).Products);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/catalog/labels/{label.Id}")).StatusCode);
        Assert.Equal(0, await ProductFormApiTests.QueryAsync(factory, db => db.DdProductToLabels.CountAsync(p => p.LabelId == label.Id)));
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/catalog/labels/{label.Id}")).StatusCode);
    }

    [MySqlTheory]
    [InlineData("/catalog/sukni?filter=new", HttpStatusCode.Created)]
    [InlineData("https://vidro.example/sale", HttpStatusCode.Created)]
    [InlineData("javascript:alert(1)", HttpStatusCode.BadRequest)]
    [InlineData("//evil.example/x", HttpStatusCode.BadRequest)]
    [InlineData("/x\" onmouseover=\"a", HttpStatusCode.BadRequest)]
    [InlineData("data:text/html,x", HttpStatusCode.BadRequest)]
    public async Task SearchPresetAddresses_AreOnlyTheShopOrHttps(string url, HttpStatusCode status)
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        Assert.Equal(status, (await client.PostAsJsonAsync("/catalog/search-presets", new SearchPresetDto(Name: "Новинки", Url: url))).StatusCode);
    }

    [MySqlFact]
    public async Task SearchPresets_AreEditedAndDeleted()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var preset = await ReadAsync<SearchPresetDto>(await client.PostAsJsonAsync("/catalog/search-presets", new SearchPresetDto(Name: "Літо", Url: "/summer", SortOrder: 2)));
        var changed = await ReadAsync<SearchPresetDto>(await client.PutAsJsonAsync($"/catalog/search-presets/{preset.Id}", preset with { Enabled = false }));
        Assert.Equal((false, "/summer"), (changed.Enabled, changed.Url));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/catalog/search-presets/{preset.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/catalog/search-presets/{preset.Id}")).StatusCode);
    }

    private static JsonObject Category(string name, int parentId = 0) => new() { ["name"] = name, ["parentId"] = parentId };

    private static async Task<CategoryEditDto> CreateAsync(HttpClient client, JsonObject body) =>
        await ReadAsync<CategoryEditDto>(await client.PostAsJsonAsync("/catalog/categories", body));

    private Task<List<(int PathId, int Level)>> PathsAsync(int categoryId) => ProductFormApiTests.QueryAsync(factory, async db =>
        (await db.CategoryPaths.Where(p => p.CategoryId == categoryId).OrderBy(p => p.Level).ToListAsync()).Select(p => (p.PathId, p.Level)).ToList());

    [MySqlFact]
    public async Task Categories_KeepTheirTreePaths_AndRefuseLoops()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var clothes = await CreateAsync(client, new JsonObject { ["name"] = "Одяг", ["parentId"] = 0, ["seoKeyword"] = "odiah" });
        var dresses = await CreateAsync(client, new JsonObject
        {
            ["name"] = "Сукні", ["parentId"] = clothes.Id, ["description"] = "<p>Лляні<script>x()</script></p>", ["productionPeriod"] = 14,
        });
        Assert.Equal(("<p>Лляні</p>", 14), (dresses.Description, dresses.ProductionPeriod));
        var summer = await CreateAsync(client, Category("Літні", dresses.Id));
        Assert.Equal([(clothes.Id, 0), (dresses.Id, 1), (summer.Id, 2)], await PathsAsync(summer.Id));
        var names = (await client.GetFromJsonAsync<List<CategoryListItem>>("/catalog/categories"))!.ToDictionary(c => c.Id, c => c.Name);
        Assert.Equal("Одяг > Сукні > Літні", names[summer.Id]);

        // Перенесення гілки перебудовує й шляхи під нею.
        var top = await ReadAsync<CategoryEditDto>(await client.PutAsJsonAsync($"/catalog/categories/{dresses.Id}", dresses with { ParentId = 0 }));
        Assert.Equal(0, top.ParentId);
        Assert.Equal([(dresses.Id, 0), (summer.Id, 1)], await PathsAsync(summer.Id));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/catalog/categories/{dresses.Id}", dresses with { ParentId = summer.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/catalog/categories", new JsonObject { ["name"] = "Дубль", ["seoKeyword"] = "odiah" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/catalog/categories", Category("Х", 9999))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/catalog/categories", Category(" "))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/catalog/categories/9999")).StatusCode);
    }

    [MySqlFact]
    public async Task OnlyTheAdminDeletesCategories_AndNotWithSubcategories()
    {
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        var parent = await CreateAsync(manager, Category("Аксесуари"));
        var child = await CreateAsync(manager, Category("Сумки", parent.Id));

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.DeleteAsync($"/catalog/categories/{child.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/catalog/categories/{parent.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/catalog/categories/{child.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/catalog/categories/{parent.Id}")).StatusCode);
        Assert.Empty(await PathsAsync(parent.Id));

        await ProductFormApiTests.WithDbAsync(factory, db => db.CategoryPaths.ExecuteDeleteAsync()); // зламане дерево
        Assert.True((await ReadAsync<CategoryRepairResult>(await admin.PostAsync("/catalog/categories/repair", null))).Categories >= 1);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync("/catalog/categories/repair", null)).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/catalog/categories")).StatusCode);
    }
}
