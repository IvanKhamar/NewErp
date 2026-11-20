using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Catalog;

namespace NewErp.Api.Endpoints;

/// <summary>Довідники каталогу: мітки й пресети пошуку (пункт 47), категорії (пункт 48). Менеджер; видалення категорій — адміністратор.</summary>
public static class CatalogDictionaryEndpoints
{
    public static void MapCatalogDictionaryEndpoints(this WebApplication app)
    {
        var catalog = app.MapGroup("/catalog").WithTags("Довідники каталогу").RequireAuthorization(Policies.Orders);

        catalog.MapGet("/labels", (CatalogDictionaryService service, CancellationToken ct) => service.LabelsAsync(ct))
            .WithSummary("Мітки товарів з кількістю товарів");
        catalog.MapPost("/labels", async (LabelDto request, CatalogDictionaryService service, CancellationToken ct) =>
            {
                var label = await service.SaveLabelAsync(null, request, ct);
                return Results.Created($"/catalog/labels/{label.Id}", label);
            })
            .WithSummary("Нова мітка (кольори — #rrggbb)");
        catalog.MapPut("/labels/{id:int}", (int id, LabelDto request, CatalogDictionaryService service, CancellationToken ct) =>
                service.SaveLabelAsync(id, request, ct))
            .WithSummary("Змінити мітку");
        catalog.MapDelete("/labels/{id:int}", async (int id, ClaimsPrincipal user, CatalogDictionaryService service, CancellationToken ct) =>
            {
                await service.DeleteLabelAsync(id, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Видалити мітку й зняти її з товарів");

        catalog.MapGet("/search-presets", (CatalogDictionaryService service, CancellationToken ct) => service.PresetsAsync(ct))
            .WithSummary("Готові пошуки вітрини");
        catalog.MapPost("/search-presets", async (SearchPresetDto request, CatalogDictionaryService service, CancellationToken ct) =>
            {
                var preset = await service.SavePresetAsync(null, request, ct);
                return Results.Created($"/catalog/search-presets/{preset.Id}", preset);
            })
            .WithSummary("Новий пресет (адреса відносна або https)");
        catalog.MapPut("/search-presets/{id:int}", (int id, SearchPresetDto request, CatalogDictionaryService service, CancellationToken ct) =>
                service.SavePresetAsync(id, request, ct))
            .WithSummary("Змінити пресет");
        catalog.MapDelete("/search-presets/{id:int}", async (int id, CatalogDictionaryService service, CancellationToken ct) =>
            {
                await service.DeletePresetAsync(id, ct);
                return Results.NoContent();
            })
            .WithSummary("Видалити пресет");

        catalog.MapGet("/categories", (CatalogDictionaryService service, CancellationToken ct) => service.CategoriesAsync(ct))
            .WithSummary("Усі категорії з батьками в назві й кількістю товарів");
        catalog.MapGet("/categories/filters", (CatalogDictionaryService service, CancellationToken ct) => service.FiltersAsync(ct))
            .WithSummary("Значення фільтрів для форми категорії («група: значення»)");
        catalog.MapPost("/categories/repair", (CatalogDictionaryService service, CancellationToken ct) => service.RepairPathsAsync(ct))
            .RequireAuthorization(Policies.CatalogBulk)
            .WithSummary("Перебудувати дерево категорій (dd_category_path)");
        catalog.MapGet("/categories/{id:int}", (int id, CatalogDictionaryService service, CancellationToken ct) => service.CategoryAsync(id, ct))
            .WithSummary("Категорія для редагування");
        catalog.MapPost("/categories", async (CategoryFormDto request, ClaimsPrincipal user, CatalogDictionaryService service, CancellationToken ct) =>
            {
                var category = await service.SaveCategoryAsync(null, request, user.GetUserId(), ct);
                return Results.Created($"/catalog/categories/{category.Id}", category);
            })
            .WithSummary("Нова категорія; HTML опису очищується");
        catalog.MapPut("/categories/{id:int}", (int id, CategoryFormDto request, ClaimsPrincipal user, CatalogDictionaryService service, CancellationToken ct) =>
                service.SaveCategoryAsync(id, request, user.GetUserId(), ct))
            .WithSummary("Змінити категорію (і перебудувати дерево)");
        catalog.MapDelete("/categories/{id:int}", async (int id, ClaimsPrincipal user, CatalogDictionaryService service, CancellationToken ct) =>
            {
                await service.DeleteCategoryAsync(id, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Policies.CatalogBulk)
            .WithSummary("Видалити категорію без підкатегорій (лише адміністратор)");
    }
}
