using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Catalog;
using NewErp.Domain.Common;

namespace NewErp.Api.Endpoints;

/// <summary>Товари магазину; той самий контракт, що й у Java-версії (ProductController) і Python-версії.</summary>
public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/catalog/products").WithTags("Товари").RequireAuthorization(Policies.Orders);

        products.MapGet("/", ([AsParameters] ProductFilter filter, ProductService service, CancellationToken ct) => service.ListAsync(filter, ct))
            .WithSummary("Товари магазину з фільтрами (пошук, категорія, мітка, увімкнені) і пагінацією");

        products.MapGet("/{id:int}", (int id, ProductService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithSummary("Картка товару: розміри із залишками (лише для читання), мітки й категорії");

        products.MapPatch("/{id:int}", (int id, UpdateProductRequest request, ProductService service, CancellationToken ct) =>
                service.UpdateAsync(id, request, ct))
            .WithSummary("Змінити ціну, увімкнення, мітки або категорії (null — без змін); dateModified з картки обов'язковий, "
                + "якщо товар змінили після цього — 409");

        products.MapGet("/labels",(ProductService service, CancellationToken ct) => service.LabelsAsync(ct))
            .WithSummary("Усі мітки товарів");

        products.MapGet("/categories", (ProductService service, CancellationToken ct) => service.CategoriesAsync(ct))
            .WithSummary("Увімкнені категорії");

        // ═══ Уся форма товару старої адмінки (Python first, пункти 43, 45 і 76) ═══

        products.MapGet("/form-lookups", (ProductEditService editor, CancellationToken ct) => editor.LookupsAsync(ct))
            .WithSummary("Довідники форми товару: категорії, мітки, фільтри, розміри, опції, атрибути, завантаження");

        products.MapPost("/", async (SaveProductRequest request, ClaimsPrincipal user, ProductEditService editor, CancellationToken ct) =>
            {
                var created = await editor.CreateAsync(request, user.GetUserId(), ct);
                return Results.Created($"/catalog/products/{created.ProductId}", created);
            })
            .WithSummary("Новий товар: вимкнений, доки його не ввімкнуть");

        products.MapPost("/remove-new", (RemoveNewRequest request, ProductEditService editor, CancellationToken ct) =>
                editor.RemoveNewAsync(request, ct))
            .WithSummary("Зняти NEW (мітку, фільтр «За темою» і тег) з усіх товарів принта за SKU");

        products.MapGet("/{id:int}/edit", (int id, ProductEditService editor, CancellationToken ct) => editor.GetAsync(id, ct))
            .WithSummary("Увесь товар для форми редагування");

        products.MapPut("/{id:int}", (int id, SaveProductRequest request, ClaimsPrincipal user, ProductEditService editor, CancellationToken ct) =>
                editor.SaveAsync(id, request, user.GetUserId(), ct))
            .WithSummary("Зберегти всю форму товару; HTML очищується; dateModified обов'язковий, змінений товар — 409");

        products.MapPost("/{id:int}/copy", async (int id, ClaimsPrincipal user, ProductEditService editor, CancellationToken ct) =>
            {
                var created = await editor.CopyAsync(id, user.GetUserId(), ct);
                return Results.Created($"/catalog/products/{created.ProductId}", created);
            })
            .WithSummary("Копія товару: вимкнена, без залишку й SEO-адреси");

        // ═══ Імпорт товарів із CSV (Python first, пункт 44): лише адміністратор ═══

        products.MapPost("/import/preview", async (HttpRequest http, ProductImportService imports, CancellationToken ct) =>
                await imports.PreviewAsync(await ReadFileAsync(http, ProductImportService.MaxBytes, ct), ct))
            .RequireAuthorization(Policies.CatalogBulk)
            .WithSummary("CSV (UTF-8; model або product_id + колонки) у полі file: що зміниться");

        products.MapPost("/import", async (HttpRequest http, ClaimsPrincipal user, ProductImportService imports, CancellationToken ct) =>
                await imports.ApplyAsync(await ReadFileAsync(http, ProductImportService.MaxBytes, ct), user.GetUserId(), ct))
            .RequireAuthorization(Policies.CatalogBulk)
            .WithSummary("Оновити товари з CSV; рядки з помилками пропускаються");

        // ═══ Картинки вітрини (Python first, пункт 75) ═══

        app.MapPost("/catalog/pictures/{kind}", async (string kind, HttpRequest http, ClaimsPrincipal user, PictureService pictures, CancellationToken ct) =>
            {
                var picture = await pictures.UploadAsync(kind, await ReadFileAsync(http, PictureService.MaxBytes, ct), user.GetUserId(), ct);
                return Results.Created(picture.Path, picture);
            })
            .WithTags("Товари")
            .RequireAuthorization(Policies.Orders)
            .WithSummary("Завантажити картинку (products, categories, banners, vouchers) у полі file; шлях — у відповіді");
    }

    /// <summary>
    /// Файл із поля file форми multipart/form-data, не більше max + 1 байта: зайвий байт каже сервісу, що файл завеликий,
    /// і великий файл не заповнює пам'ять. Форма безпечна від чужих сайтів: cookie входу має SameSite=Strict.
    /// </summary>
    private static async Task<byte[]> ReadFileAsync(HttpRequest http, int max, CancellationToken ct)
    {
        if (!http.HasFormContentType)
            throw new DomainException("Надішліть файл у полі file (multipart/form-data).");
        var form = await http.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? throw new DomainException("Надішліть файл у полі file (multipart/form-data).");
        await using var stream = file.OpenReadStream();
        var buffer = new byte[Math.Min(file.Length, max + 1L)];
        var read = 0;
        while (read < buffer.Length && await stream.ReadAsync(buffer.AsMemory(read), ct) is var n and > 0)
            read += n;
        return read == buffer.Length ? buffer : buffer[..read];
    }
}
