using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Stock;

namespace NewErp.Api.Endpoints;

public static class StockEndpoints
{
    public static void MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var stock = app.MapGroup("/stock").WithTags("Склад: розхідники").RequireAuthorization(Policies.Stock);

        stock.MapGet("/categories", (IStockRepository repo, CancellationToken ct) => repo.ListCategoriesAsync(ct));

        stock.MapPost("/categories", async (CreateStockCategoryRequest request, StockService service, CancellationToken ct) =>
        {
            var id = await service.CreateCategoryAsync(request, ct);
            return Results.Created($"/stock/categories/{id}", new { id });
        });

        stock.MapGet("/suppliers", (IStockRepository repo, CancellationToken ct) => repo.ListSuppliersAsync(ct));

        stock.MapPost("/suppliers", async (CreateStockSupplierRequest request, StockService service, CancellationToken ct) =>
        {
            var id = await service.CreateSupplierAsync(request, ct);
            return Results.Created($"/stock/suppliers/{id}", new { id });
        });

        stock.MapGet("/items", (IStockRepository repo, [AsParameters] StockItemFilter filter, CancellationToken ct) =>
            repo.ListItemsAsync(filter, ct))
            .WithSummary("Позиції складу; belowMinimumOnly=true показує те, що треба дозамовити");

        stock.MapGet("/reorder", (ReorderService service, CancellationToken ct) => service.GetReportAsync(ct))
            .WithSummary("Що дозамовити: позиції нижче мінімуму за постачальниками, до 2× мінімуму, з готовим текстом замовлення");

        stock.MapGet("/items/{id:int}",async (int id, IStockRepository repo, CancellationToken ct) =>
            await repo.GetItemDtoAsync(id, ct) is { } item ? Results.Ok(item) : Results.NotFound());

        stock.MapPost("/items", async (CreateStockItemRequest request, StockService service, CancellationToken ct) =>
        {
            var id = await service.CreateItemAsync(request, ct);
            return Results.Created($"/stock/items/{id}", new { id });
        });

        stock.MapPost("/items/{id:int}/adjustments", (int id, AdjustStockRequest request, ClaimsPrincipal user, StockService service, CancellationToken ct) =>
            service.AdjustAsync(id, request, user.GetUserId(), user.GetUsername(), ct))
            .WithSummary("Прихід (+) або витрата (-) з записом у журнал руху");

        stock.MapPut("/items/{id:int}/min-quantity", async (int id, decimal minQuantity, StockService service, CancellationToken ct) =>
        {
            await service.SetMinQuantityAsync(id, minQuantity, ct);
            return Results.NoContent();
        });

        stock.MapGet("/items/{id:int}/movements", (int id, IStockRepository repo, CancellationToken ct) =>
            repo.ListMovementsAsync(id, ct))
            .WithSummary("Журнал руху позиції");
    }
}