using NewErp.Api.Auth;
using NewErp.Application.Catalog;
using NewErp.Domain.Catalog;

namespace NewErp.Api.Endpoints;

public static class ProductSortEndpoints
{
    public static void MapProductSortEndpoints(this IEndpointRouteBuilder app)
    {
        var sort = app.MapGroup("/catalog/sort").WithTags("Порядок товарів").RequireAuthorization(Policies.Orders);

        sort.MapGet("/groups", (ProductSortService service, CancellationToken ct) => service.ListGroupsAsync(ct))
            .WithSummary("Групи товарів у порядку перерахунку: теми, категорії (з кольорами окремо), мітки, панель допродажу");

        sort.MapGet("/preview", (SortGroupType type, string key, int? days, DateOnly? from, DateOnly? to, ProductSortService service, CancellationToken ct) =>
            service.PreviewAsync(type, key, new SalesPeriod(days, from, to), ct))
            .WithSummary("Новий порядок групи без запису: продажі за період (days або from–to), закріплені товари, поточна й нова позиція");

        sort.MapPost("/apply", (ApplySortRequest request, ProductSortService service, CancellationToken ct) => service.ApplyAsync(request, ct))
            .WithSummary("Записати готовий порядок групи (зі знімком попереднього)");

        sort.MapPost("/recalculate", (RecalculateGroupRequest request, ProductSortService service, CancellationToken ct) =>
            service.RecalculateGroupAsync(request, ct))
            .WithSummary("Перерахувати й записати одну групу");

        sort.MapPost("/recalculate-all", (SalesPeriod? period, ProductSortService service, CancellationToken ct) =>
            service.RecalculateAllAsync(period, ct))
            .WithSummary("Перерахувати всі групи (один знімок перед початком)");

        sort.MapGet("/snapshots", (ProductSortService service, CancellationToken ct) => service.ListSnapshotsAsync(ct))
            .WithSummary("Останні 50 знімків порядку");

        sort.MapPost("/snapshots/{id:int}/restore", async (int id, ProductSortService service, CancellationToken ct) =>
        {
            await service.RestoreSnapshotAsync(id, ct);
            return Results.NoContent();
        })
            .WithSummary("Повернути порядок товарів зі знімка");

        sort.MapGet("/pins", (ProductSortService service, CancellationToken ct) => service.ListPinsAsync(ct))
            .WithSummary("Усі закріплені вручну позиції");

        sort.MapPost("/pins", async (SetPinRequest request, ProductSortService service, CancellationToken ct) =>
        {
            await service.SetPinAsync(request, ct);
            return Results.NoContent();
        })
            .WithSummary("Закріпити товар на позиції в групі (попередній пін товару чи позиції замінюється)");

        sort.MapDelete("/pins/{id:int}", async (int id, ProductSortService service, CancellationToken ct) =>
        {
            await service.RemovePinAsync(id, ct);
            return Results.NoContent();
        });

        sort.MapGet("/crosssell-metric", (ProductSortService service, CancellationToken ct) => service.GetCrossSellMetricAsync(ct));

        sort.MapPut("/crosssell-metric", (CrossSellMetricDto metric, ProductSortService service, CancellationToken ct) =>
            service.SetCrossSellMetricAsync(metric, ct))
            .WithSummary("Популярність у панелі допродажу: panelOnly=true — лише продажі через панель");

        sort.MapGet("/products", (string? search, ProductSortService service, CancellationToken ct) => service.SearchProductsAsync(search, ct))
            .WithSummary("Пошук увімкнених товарів за назвою (для закріплення)");
    }
}
