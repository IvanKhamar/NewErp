using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;

using NewErp.Application.Catalog;

namespace NewErp.IntegrationTests;

/// <summary>Фільтри, атрибути, опції, виробники й завантаження (пункт 49). Перенесено з Python tests/test_catalog_references.py.</summary>
public class CatalogReferenceApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [MySqlTheory]
    [InlineData("filters")]
    [InlineData("attributes")]
    [InlineData("options")]
    public async Task GroupsAndValues_AreEdited_AndUsedOnesKept(string kind)
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var created = await client.PostAsJsonAsync($"/catalog/references/{kind}", new ReferenceGroupDto(Name: " Колір ", SortOrder: 1, Type: "radio"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var group = (await created.Content.ReadFromJsonAsync<ReferenceGroupDto>())!;
        Assert.Equal(("Колір", 0), (group.Name, group.Items.Count));
        if (kind == "options")
            Assert.Equal("radio", group.Type);

        var withValue = await ReadAsync<ReferenceGroupDto>(await client.PostAsJsonAsync($"/catalog/references/{kind}/{group.Id}/values", new ReferenceItemDto(Name: "Червоний")));
        var value = Assert.Single(withValue.Items);
        var renamed = await ReadAsync<ReferenceGroupDto>(await client.PutAsJsonAsync($"/catalog/references/{kind}/{group.Id}/values/{value.Id}", new ReferenceItemDto(Name: "Бордо", SortOrder: 2)));
        Assert.Equal([("Бордо", 2, 0)], renamed.Items.Select(i => (i.Name, i.SortOrder, i.Products)));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/catalog/references/{kind}/{group.Id}/values/99999", new ReferenceItemDto(Name: "x"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/catalog/references/{kind}/{group.Id}/values", new ReferenceItemDto(Name: " "))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/catalog/references/{kind}/values/{value.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/catalog/references/{kind}/{group.Id}")).StatusCode);
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<ReferenceGroupDto>>($"/catalog/references/{kind}"))!, g => g.Id == group.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/catalog/references/colors")).StatusCode); // не довідник
    }

    [MySqlFact]
    public async Task AValueOnAProduct_CannotBeDeleted_AndShowsInTheProductForm()
    {
        await ProductFormApiTests.SeedAsync(factory);
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var group = await ReadAsync<ReferenceGroupDto>(await client.PostAsJsonAsync("/catalog/references/attributes", new ReferenceGroupDto(Name: "Склад")));
        var attributeId = (await ReadAsync<ReferenceGroupDto>(await client.PostAsJsonAsync($"/catalog/references/attributes/{group.Id}/values", new ReferenceItemDto(Name: "Тканина")))).Items[0].Id;
        var download = await ReadAsync<DownloadDto>(await client.PostAsJsonAsync("/catalog/downloads", new DownloadDto(Name: "Догляд", Filename: "care.pdf")));
        Assert.Equal("care.pdf", download.Mask);

        var lookups = (await client.GetFromJsonAsync<ProductFormLookups>("/catalog/products/form-lookups"))!;
        Assert.Contains(lookups.Attributes, a => a.Id == attributeId && a.Name == "Склад: Тканина");
        var productId = await ProductFormApiTests.CreateAsync(client, new JsonObject
        {
            ["name"] = "Сукня з атрибутом", ["model"] = "ATTR-1", ["downloadIds"] = new JsonArray(download.Id),
            ["attributes"] = new JsonArray(new JsonObject { ["attributeId"] = attributeId, ["text"] = "Льон 100%" }),
        });
        var product = await ProductFormApiTests.EditJsonAsync(client, productId);
        Assert.Equal(attributeId, product["attributes"]![0]!["attributeId"]!.GetValue<int>());

        var refused = await client.DeleteAsync($"/catalog/references/attributes/values/{attributeId}");
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("1 товарах", await refused.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/catalog/references/attributes/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/catalog/downloads/{download.Id}")).StatusCode);
    }

    [MySqlFact]
    public async Task ManufacturersAndDownloads()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var created = await client.PostAsJsonAsync("/catalog/manufacturers", new ManufacturerDto(Name: "Vidro", Image: "catalog/brands/vidro.png"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var maker = (await created.Content.ReadFromJsonAsync<ManufacturerDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/catalog/manufacturers", new ManufacturerDto(Name: "X", Image: "../x.png"))).StatusCode);
        Assert.Equal(3, (await ReadAsync<ManufacturerDto>(await client.PutAsJsonAsync($"/catalog/manufacturers/{maker.Id}", maker with { SortOrder = 3 }))).SortOrder);

        await SetManufacturerAsync(maker.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/catalog/manufacturers/{maker.Id}")).StatusCode);
        await SetManufacturerAsync(0);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/catalog/manufacturers/{maker.Id}")).StatusCode);

        foreach (var filename in new[] { "../etc/passwd", "a/b.pdf", "" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/catalog/downloads", new DownloadDto(Name: "Файл", Filename: filename))).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/catalog/manufacturers")).StatusCode);
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/catalog/downloads")).StatusCode);
    }

    private Task SetManufacturerAsync(int manufacturerId) => ProductFormApiTests.WithDbAsync(factory, db =>
        db.DdProducts.Where(p => p.ProductId == ApiFactory.CategoryProductId).ExecuteUpdateAsync(s => s.SetProperty(p => p.ManufacturerId, manufacturerId)));
}
