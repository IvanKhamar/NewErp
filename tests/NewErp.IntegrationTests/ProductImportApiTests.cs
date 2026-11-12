using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

using NewErp.Application.Catalog;

namespace NewErp.IntegrationTests;

/// <summary>Імпорт товарів із CSV (пункт 44): спершу перегляд, потім запис; лише адміністратор. Перенесено з Python tests/test_catalog_prices.py.</summary>
public class ProductImportApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task CsvImport_UpdatesByModelOrId_AndReportsBadRows()
    {
        var ids = await ProductsAsync();
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        const string csv = "model;price;status;name;sku\n"
            + "PR-1;1250,50;0;Нова назва;\n"  // порожня комірка лишає значення
            + "PR-3;;;;SKU-3\n"
            + "NOPE;100;;;\n"
            + "PR-2;дорого;;;\n";
        var preview = (await (await UploadAsync(admin, "/catalog/products/import/preview", csv)).Content.ReadFromJsonAsync<ImportPreview>())!;
        Assert.Equal((2, 0), (preview.Errors, preview.Unchanged));
        Assert.Equal(4, preview.Rows.Count);
        var (first, third, missing, bad) = (preview.Rows[0], preview.Rows[1], preview.Rows[2], preview.Rows[3]);
        Assert.Equal(new Dictionary<string, string> { ["price"] = "1000.0000 → 1250.50", ["name"] = "Товар PR-1 → Нова назва" }, first.Changes); // status і так 0
        Assert.Equal(new Dictionary<string, string> { ["sku"] = " → SKU-3" }, third.Changes);
        Assert.Contains("імпорт лише оновлює", missing.Error);
        Assert.Contains("не число", bad.Error);

        var applied = (await (await UploadAsync(admin, "/catalog/products/import", csv)).Content.ReadFromJsonAsync<ImportResult>())!;
        Assert.Equal(new ImportResult(2, 2), applied);
        var product = (await admin.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{ids[0]}/edit"))!;
        Assert.Equal((1250.5m, "Нова назва", false), (product.Price, product.Name, product.Enabled));
        Assert.Equal("SKU-3", (await admin.GetFromJsonAsync<ProductEditDto>($"/catalog/products/{ids[2]}/edit"))!.Sku);
    }

    public static TheoryData<byte[], string> BadFiles => new()
    {
        { "price;color\n1;red\n"u8.ToArray(), "Невідомі колонки" },
        { "price\n1\n"u8.ToArray(), "model або product_id" },
        { [.. "model;price\n"u8.ToArray(), 0xD1, 0xF3, 0xEA, 0xED, 0xFF, (byte)';', (byte)'1', (byte)'\n'], "UTF-8" }, // cp1251
        { [], "порожній" },
        { Encoding.UTF8.GetBytes("model;price\n" + string.Concat(Enumerable.Repeat("X;1\n", 5001))), "5000" },
        { Enumerable.Repeat((byte)'x', 1_000_001).ToArray(), "МБ" },
    };

    [MySqlTheory]
    [MemberData(nameof(BadFiles))]
    public async Task CsvImport_RefusesBadFiles(byte[] content, string message)
    {
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        var response = await PostFileAsync(admin, "/catalog/products/import/preview", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync());
    }

    [MySqlFact]
    public async Task Import_IsForTheAdminOnly()
    {
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostFileAsync(manager, "/catalog/products/import", "model;price\n"u8.ToArray())).StatusCode);
    }

    private async Task<List<int>> ProductsAsync()
    {
        await ProductFormApiTests.SeedAsync(factory);
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        var ids = new List<int>();
        foreach (var (model, price) in new[] { ("PR-1", 1000), ("PR-2", 1490), ("PR-3", 700) })
            ids.Add(await ProductFormApiTests.CreateAsync(manager, new JsonObject
            {
                ["name"] = $"Товар {model}", ["model"] = model, ["price"] = price, ["categoryIds"] = new JsonArray(ApiFactory.CategoryId),
            }));
        return ids;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string path, string text) =>
        PostFileAsync(client, path, [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)]);

    internal static Task<HttpResponseMessage> PostFileAsync(HttpClient client, string path, byte[] content, string name = "products.csv")
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(content), "file", name } };
        return client.PostAsync(path, form);
    }
}
