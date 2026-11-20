using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Catalog;

namespace NewErp.Api.Endpoints;

/// <summary>Фільтри, атрибути, опції, виробники й завантаження (пункт 49): менеджер і адміністратор.</summary>
public static class CatalogReferenceEndpoints
{
    public static void MapCatalogReferenceEndpoints(this WebApplication app)
    {
        var catalog = app.MapGroup("/catalog").WithTags("Довідники каталогу").RequireAuthorization(Policies.Orders);

        catalog.MapGet("/references/{kind}", (string kind, CatalogReferenceService service, CancellationToken ct) =>
                service.GroupsAsync(CatalogReferenceService.Kind(kind), ct))
            .WithSummary("Групи фільтрів, атрибутів чи опцій зі значеннями");
        catalog.MapPost("/references/{kind}", async (string kind, ReferenceGroupDto request, CatalogReferenceService service, CancellationToken ct) =>
            {
                var group = await service.SaveGroupAsync(CatalogReferenceService.Kind(kind), null, request, ct);
                return Results.Created($"/catalog/references/{kind}/{group.Id}", group);
            })
            .WithSummary("Нова група (для опцій — і тип)");
        catalog.MapPut("/references/{kind}/{groupId:int}", (string kind, int groupId, ReferenceGroupDto request, CatalogReferenceService service, CancellationToken ct) =>
                service.SaveGroupAsync(CatalogReferenceService.Kind(kind), groupId, request, ct))
            .WithSummary("Змінити групу");
        catalog.MapDelete("/references/{kind}/{groupId:int}", async (string kind, int groupId, ClaimsPrincipal user, CatalogReferenceService service, CancellationToken ct) =>
            {
                await service.DeleteGroupAsync(CatalogReferenceService.Kind(kind), groupId, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Видалити групу без значень у товарах");
        catalog.MapPost("/references/{kind}/{groupId:int}/values", async (string kind, int groupId, ReferenceItemDto request, CatalogReferenceService service, CancellationToken ct) =>
            {
                var group = await service.SaveItemAsync(CatalogReferenceService.Kind(kind), groupId, null, request, ct);
                return Results.Created($"/catalog/references/{kind}/{groupId}", group);
            })
            .WithSummary("Нове значення групи");
        catalog.MapPut("/references/{kind}/{groupId:int}/values/{valueId:int}", (string kind, int groupId, int valueId, ReferenceItemDto request, CatalogReferenceService service, CancellationToken ct) =>
                service.SaveItemAsync(CatalogReferenceService.Kind(kind), groupId, valueId, request, ct))
            .WithSummary("Змінити значення");
        catalog.MapDelete("/references/{kind}/values/{valueId:int}", async (string kind, int valueId, ClaimsPrincipal user, CatalogReferenceService service, CancellationToken ct) =>
            {
                await service.DeleteItemAsync(CatalogReferenceService.Kind(kind), valueId, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Видалити значення, якого немає в товарах");

        catalog.MapGet("/manufacturers", (CatalogReferenceService service, CancellationToken ct) => service.ManufacturersAsync(ct))
            .WithSummary("Виробники з кількістю товарів");
        catalog.MapPost("/manufacturers", async (ManufacturerDto request, CatalogReferenceService service, CancellationToken ct) =>
            {
                var manufacturer = await service.SaveManufacturerAsync(null, request, ct);
                return Results.Created($"/catalog/manufacturers/{manufacturer.Id}", manufacturer);
            })
            .WithSummary("Новий виробник");
        catalog.MapPut("/manufacturers/{id:int}", (int id, ManufacturerDto request, CatalogReferenceService service, CancellationToken ct) =>
                service.SaveManufacturerAsync(id, request, ct))
            .WithSummary("Змінити виробника");
        catalog.MapDelete("/manufacturers/{id:int}", async (int id, ClaimsPrincipal user, CatalogReferenceService service, CancellationToken ct) =>
            {
                await service.DeleteManufacturerAsync(id, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Видалити виробника без товарів");

        catalog.MapGet("/downloads", (CatalogReferenceService service, CancellationToken ct) => service.DownloadsAsync(ct))
            .WithSummary("Файли для покупців (записи; самі файли — на вітрині)");
        catalog.MapPost("/downloads", async (DownloadDto request, CatalogReferenceService service, CancellationToken ct) =>
            {
                var download = await service.SaveDownloadAsync(null, request, ct);
                return Results.Created($"/catalog/downloads/{download.Id}", download);
            })
            .WithSummary("Нове завантаження (ім'я файлу без папок)");
        catalog.MapPut("/downloads/{id:int}", (int id, DownloadDto request, CatalogReferenceService service, CancellationToken ct) =>
                service.SaveDownloadAsync(id, request, ct))
            .WithSummary("Змінити завантаження");
        catalog.MapDelete("/downloads/{id:int}", async (int id, ClaimsPrincipal user, CatalogReferenceService service, CancellationToken ct) =>
            {
                await service.DeleteDownloadAsync(id, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Видалити завантаження без товарів");
    }
}
