using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;

using NewErp.Application.Catalog;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>Опції, крім розмірів, у формі товару (пункт 76): кольори й довжини окремо від розмірів. Перенесено з Python tests/test_product_options.py.</summary>
public class ProductOptionsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int Colour = 20, Engraving = 21, Length = 22;
    private const int Red = 201, Blue = 202, Midi = 221;

    private static JsonObject Form(params (string Key, JsonNode? Value)[] changes)
    {
        var form = new JsonObject { ["model"] = "OPT-1", ["sku"] = "P", ["price"] = 1000, ["name"] = "Сукня", ["image"] = "" };
        foreach (var (key, value) in changes)
            form[key] = value;
        return form;
    }

    private static JsonObject ColourOption(bool required = true, params JsonObject[] values) =>
        new() { ["optionId"] = Colour, ["required"] = required, ["values"] = new JsonArray([.. values]) };

    private static JsonObject Value(int id, decimal price = 0, string? prefix = null)
    {
        var value = new JsonObject { ["optionValueId"] = id, ["price"] = price };
        if (prefix is not null)
            value["pricePrefix"] = prefix;
        return value;
    }

    [MySqlFact]
    public async Task Options_AreAddedChangedAndRemovedInPlace()
    {
        var client = await ReadyAsync();
        var productId = await ProductFormApiTests.CreateAsync(client, Form(
            ("sizes", new JsonArray(new JsonObject { ["optionValueId"] = ApiFactory.SizeMOptionValueId, ["price"] = 0 })),
            ("options", new JsonArray(
                ColourOption(true, Value(Red, 100), Value(Blue, 50, "-")),
                new JsonObject { ["optionId"] = Engraving, ["required"] = false, ["value"] = "  Ім'я  " }))));

        var product = (await client.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{productId}/edit"))!;
        Assert.Equal([ApiFactory.SizeMOptionValueId], product.Sizes.Select(s => s.OptionValueId)); // не змішано
        var byId = product.Options.ToDictionary(o => o.OptionId);
        Assert.Equal(("Ім'я", false), (byId[Engraving].Value, byId[Engraving].Required));
        var values = byId[Colour].Values.ToDictionary(v => v.OptionValueId);
        Assert.Equal(("Червоний", 100m, "+"), (values[Red].Name, values[Red].Price, values[Red].PricePrefix));
        Assert.Equal("-", values[Blue].PricePrefix);

        var first = (await RowsAsync(productId)).ToDictionary(r => r.OptionValueId);
        Assert.Equal((false, 0), (first[Red].Subtract, first[Red].Quantity)); // не лічиться, тож вітрина показує його й на нулі
        Assert.True(first[ApiFactory.SizeMOptionValueId].Subtract);

        var changed = await ProductFormApiTests.EditJsonAsync(client, productId);
        changed["options"] = new JsonArray(ColourOption(true, Value(Red, 120, "-")));
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/catalog/products/{productId}", changed)).StatusCode);
        var after = (await RowsAsync(productId)).ToDictionary(r => r.OptionValueId);
        Assert.DoesNotContain(Blue, after.Keys);
        Assert.Equal(first[Red].ProductOptionValueId, after[Red].ProductOptionValueId); // той самий рядок: на нього посилаються старі замовлення
        Assert.Equal((120m, "-"), (after[Red].Price, after[Red].PricePrefix));
        Assert.DoesNotContain(Engraving, (await ParentsAsync(productId)).Keys); // пропущена — теж прибрана

        var fresh = await ProductFormApiTests.EditJsonAsync(client, productId);
        fresh["options"] = new JsonArray();
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/catalog/products/{productId}", fresh)).StatusCode);
        Assert.DoesNotContain(Colour, (await ParentsAsync(productId)).Keys); // вітрина не питатиме про порожній список
        Assert.Equal([ApiFactory.SizeMOptionValueId], (await RowsAsync(productId)).Select(r => r.OptionValueId));
    }

    /// <summary>Колись колір читався як розмір: збереження відмовляло товару або мовчки губило колір.</summary>
    [MySqlFact]
    public async Task SavingSizes_LeavesOtherOptionsAlone()
    {
        var client = await ReadyAsync();
        var productId = await ProductFormApiTests.CreateAsync(client, Form(("options", new JsonArray(ColourOption(true, Value(Red))))));
        var product = await ProductFormApiTests.EditJsonAsync(client, productId);
        Assert.Empty(product["sizes"]!.AsArray());
        product["sizes"] = new JsonArray(new JsonObject { ["optionValueId"] = ApiFactory.SizeMOptionValueId, ["price"] = 0 });
        var saved = await client.PutAsJsonAsync($"/catalog/products/{productId}", product);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal([ApiFactory.SizeMOptionValueId, Red], (await RowsAsync(productId)).Select(r => r.OptionValueId).Order());
    }

    [MySqlFact]
    public async Task AValueWithStock_Stays()
    {
        var client = await ReadyAsync();
        var productId = await ProductFormApiTests.CreateAsync(client, Form(("options", new JsonArray(ColourOption(true, Value(Red))))));
        await ProductFormApiTests.WithDbAsync(factory, db =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dd_product_option_value SET quantity = 2 WHERE product_id = {productId}"));
        var product = await ProductFormApiTests.EditJsonAsync(client, productId);
        var withoutOptions = product.DeepClone().AsObject();
        withoutOptions["options"] = new JsonArray();
        var refused = await client.PutAsJsonAsync($"/catalog/products/{productId}", withoutOptions);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Червоний", await refused.Content.ReadAsStringAsync());
        Assert.Equal(2, (await RowsAsync(productId))[0].Quantity);

        var kept = await client.PutAsJsonAsync($"/catalog/products/{productId}", product);
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        Assert.Equal(2, (await RowsAsync(productId))[0].Quantity); // форма ніколи не пише залишок
    }

    public static TheoryData<string> BadOptions => new()
    {
        """[{"optionId": 999, "values": [{"optionValueId": 201}]}]""",  // такої опції немає
        """[{"optionId": 11, "values": [{"optionValueId": 47}]}]""",  // розміри мають свою частину
        """[{"optionId": 20, "values": [{"optionValueId": 221}]}]""",  // значення іншої опції
        """[{"optionId": 20, "values": [{"optionValueId": 201}, {"optionValueId": 201}]}]""",
        """[{"optionId": 20, "values": [{"optionValueId": 201}]}, {"optionId": 20, "values": [{"optionValueId": 202}]}]""",
        """[{"optionId": 20, "values": []}]""",  // список без вибору
        """[{"optionId": 20, "values": [{"optionValueId": 201, "price": -5}]}]""",
        """[{"optionId": 20, "values": [{"optionValueId": 201, "pricePrefix": "*"}]}]""",
        """[{"optionId": 21, "values": [{"optionValueId": 201}]}]""",  // текстова опція без списку
        """[{"optionId": 21, "value": "<script>alert(1)</script>"}]""",
    };

    [MySqlTheory]
    [MemberData(nameof(BadOptions))]
    public async Task BadOptions_AreRefused(string options)
    {
        var client = await ReadyAsync();
        var response = await client.PostAsJsonAsync("/catalog/products", Form(("options", JsonNode.Parse(options))));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [MySqlFact]
    public async Task ACopy_HasTheOptions_WithoutStock()
    {
        var client = await ReadyAsync();
        var sourceId = await ProductFormApiTests.CreateAsync(client, Form(("options", new JsonArray(ColourOption(true, Value(Blue, 70))))));
        await ProductFormApiTests.WithDbAsync(factory, db =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dd_product_option_value SET quantity = 5 WHERE product_id = {sourceId}"));
        var copied = await client.PostAsync($"/catalog/products/{sourceId}/copy", null);
        var copyId = (await copied.Content.ReadFromJsonAsync<CreatedProductDto>())!.ProductId;
        var value = Assert.Single((await client.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{copyId}/edit"))!.Options[0].Values);
        Assert.Equal((Blue, 70m, 0), (value.OptionValueId, value.Price, value.Quantity));
    }

    [MySqlFact]
    public async Task Lookups_LeaveSizesOut_AndTheFormIsClosedToOtherRoles()
    {
        var client = await ReadyAsync();
        var lookups = (await client.GetFromJsonAsync<ProductFormLookups>("/catalog/products/form-lookups"))!;
        var byId = lookups.Options.ToDictionary(o => o.Id);
        Assert.Equal([Colour, Engraving, Length], byId.Keys.Order());
        Assert.True(byId[Colour].HasValues);
        Assert.Equal(["Червоний", "Синій"], byId[Colour].Values.Select(v => v.Name));
        Assert.False(byId[Engraving].HasValues);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.PostAsJsonAsync("/catalog/products", Form())).StatusCode);
    }

    private async Task<HttpClient> ReadyAsync()
    {
        await ProductFormApiTests.WithDbAsync(factory, async db =>
        {
            if (await db.CatalogOptions.AnyAsync(o => o.OptionId == Colour))
                return;
            foreach (var (id, type, name) in new[] { (Colour, "select", "Колір"), (Engraving, "text", "Гравіювання"), (Length, "radio", "Довжина") })
            {
                db.CatalogOptions.Add(new CatalogOption { OptionId = id, Type = type, SortOrder = id });
                db.DdOptionDescriptions.Add(new DdOptionDescription { OptionId = id, LanguageId = ApiFactory.LanguageId, Name = name });
            }
            db.CatalogOptions.Add(new CatalogOption { OptionId = 11, Type = "radio", SortOrder = 0 }); // опція розмірів загального сіду
            foreach (var (id, option, name) in new[] { (Red, Colour, "Червоний"), (Blue, Colour, "Синій"), (Midi, Length, "Міді") })
            {
                db.CatalogOptionValues.Add(new CatalogOptionValue { OptionValueId = id, OptionId = option, Image = "", SortOrder = id });
                db.DdOptionValueDescriptions.Add(new DdOptionValueDescription { OptionValueId = id, LanguageId = ApiFactory.LanguageId, OptionId = option, Name = name });
            }
            await db.SaveChangesAsync();
        });
        return await factory.LoginAsync(ApiFactory.Manager);
    }

    private async Task<List<DdProductOptionValue>> RowsAsync(int productId)
    {
        List<DdProductOptionValue> rows = [];
        await ProductFormApiTests.WithDbAsync(factory, async db =>
            rows = await db.DdProductOptionValues.AsNoTracking().Where(v => v.ProductId == productId).OrderBy(v => v.OptionValueId).ToListAsync());
        return rows;
    }

    private async Task<Dictionary<int, DdProductOption>> ParentsAsync(int productId)
    {
        Dictionary<int, DdProductOption> parents = [];
        await ProductFormApiTests.WithDbAsync(factory, async db =>
            parents = await db.DdProductOptions.AsNoTracking().Where(o => o.ProductId == productId).ToDictionaryAsync(o => o.OptionId));
        return parents;
    }
}
