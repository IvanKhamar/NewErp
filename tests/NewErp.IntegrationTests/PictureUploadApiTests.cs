using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using NewErp.Application.Auth;
using NewErp.Application.Catalog;

namespace NewErp.IntegrationTests;

/// <summary>
/// Завантаження картинок (пункт 75): тип — за вмістом, назва — від сервера, і лише всередині папки.
/// Перенесено з Python tests/test_catalog_pictures.py.
/// </summary>
public sealed class PictureUploadApiTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, .. Enumerable.Repeat((byte)'0', 64)];

    private readonly string _folder = Directory.CreateTempSubdirectory("newerp-pictures-").FullName;

    [MySqlFact]
    public async Task TheServerMakesTheName_WhateverTheBrowserSays()
    {
        var client = await LoginAsync(WithFolder(), ApiFactory.Manager);
        // Назва й тип від браузера брешуть; жодне не використовується.
        var response = await ProductImportApiTests.PostFileAsync(client, "/catalog/pictures/products", Jpeg, "../../shell.php");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var path = (await response.Content.ReadFromJsonAsync<PictureDto>())!.Path;
        Assert.StartsWith("catalog/products/", path);
        Assert.EndsWith(".jpg", path);
        Assert.DoesNotContain("shell", path);
        Assert.Equal(Jpeg, await File.ReadAllBytesAsync(Path.Combine(_folder, path)));
        Assert.Empty(Directory.GetFiles(_folder, "*.php", SearchOption.AllDirectories));

        var second = (await (await ProductImportApiTests.PostFileAsync(client, "/catalog/pictures/products", Jpeg, "a.jpg"))
            .Content.ReadFromJsonAsync<PictureDto>())!.Path;
        Assert.NotEqual(path, second); // той самий файл двічі ніколи не перезаписує
    }

    [MySqlFact]
    public async Task RefusedUploads_LeaveNothing()
    {
        var client = await LoginAsync(WithFolder(), ApiFactory.Manager);
        (string Kind, byte[] Content)[] refused =
        [
            ("products", "<?php echo 1;"u8.ToArray()),
            ("products", [.. Jpeg, .. new byte[PictureService.MaxBytes]]),
            ("..%2F..%2Fetc", Jpeg),
            ("everything", Jpeg),
        ];
        foreach (var (kind, content) in refused)
        {
            var response = await ProductImportApiTests.PostFileAsync(client, $"/catalog/pictures/{kind}", content, "a.jpg");
            Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, $"{kind}: {response.StatusCode}");
        }
        Assert.Empty(Directory.GetFiles(_folder, "*", SearchOption.AllDirectories));
    }

    [MySqlFact]
    public async Task ItIsOff_WithoutTheFolder_AndClosedToOtherRoles()
    {
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        var response = await ProductImportApiTests.PostFileAsync(manager, "/catalog/pictures/products", Jpeg, "a.jpg");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Pictures:ImageDir", await response.Content.ReadAsStringAsync());
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ProductImportApiTests.PostFileAsync(storekeeper, "/catalog/pictures/products", Jpeg, "a.jpg")).StatusCode);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private WebApplicationFactory<Program> WithFolder() => factory.WithWebHostBuilder(b => b.UseSetting("Pictures:ImageDir", _folder));

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app, string username)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/auth/login", new LoginRequest(username, ApiFactory.Password))).EnsureSuccessStatusCode();
        return client;
    }
}
