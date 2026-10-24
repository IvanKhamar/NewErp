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

        MapPurchaseOrders(stock.MapGroup("/purchase-orders").WithTags("Склад: замовлення постачальникам"));

        stock.MapGet("/items/{id:int}", async (int id, IStockRepository repo, CancellationToken ct) =>
            await repo.GetItemDtoAsync(id, ct) is { } item ? Results.Ok(item) : Results.NotFound());

        stock.MapPost("/items", async (SaveStockItemRequest request, StockService service, CancellationToken ct) =>
        {
            var id = await service.CreateItemAsync(request, ct);
            return Results.Created($"/stock/items/{id}", new { id });
        });

        stock.MapPut("/items/{id:int}", async (int id, SaveStockItemRequest request, StockService service, CancellationToken ct) =>
        {
            await service.UpdateItemAsync(id, request, ct);
            return Results.NoContent();
        })
            .WithSummary("Змінити позицію: назва, одиниця, категорія, постачальник, мінімум, ціна (залишок — лише через adjustments)");

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

        MapLocations(stock);
    }

    private static void MapLocations(RouteGroupBuilder stock)
    {
        stock.MapGet("/warehouses", (StockLocationService service, CancellationToken ct) => service.WarehousesAsync(ct))
            .WithSummary("Склади для вибору місця (довідник «Склади й пункти видачі»), без основного")
            .WithTags("Склад: за складами");

        stock.MapGet("/by-warehouse", (StockLocationService service, CancellationToken ct) => service.ByWarehouseAsync(ct))
            .WithSummary("Залишки за місцями: основний склад і склади, де щось лежить, з вартістю кожного")
            .WithTags("Склад: за складами");

        stock.MapGet("/items/{id:int}/places", (int id, StockLocationService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithSummary("Де лежить позиція: основний склад (warehouseId = null) і склади")
            .WithTags("Склад: за складами");

        stock.MapPost("/items/{id:int}/transfers", (int id, TransferStockRequest request, ClaimsPrincipal user, StockLocationService service, CancellationToken ct) =>
            service.TransferAsync(id, request, user.GetUserId(), user.GetUsername(), ct))
            .WithSummary("Перемістити між складами (null — основний); загальний залишок не змінюється")
            .WithTags("Склад: за складами");

        stock.MapGet("/items/{id:int}/transfers", (int id, StockLocationService service, CancellationToken ct) => service.TransfersAsync(id, ct))
            .WithSummary("Журнал переміщень позиції")
            .WithTags("Склад: за складами");

        stock.MapPost("/items/{id:int}/warehouses/{warehouseId:int}/adjustments",
            (int id, int warehouseId, AdjustStockRequest request, ClaimsPrincipal user, StockLocationService service, CancellationToken ct) =>
                service.AdjustAsync(id, warehouseId, request, user.GetUserId(), user.GetUsername(), ct))
            .WithSummary("Прихід (+) або витрата (-) на конкретному складі, з записом у журнал руху")
            .WithTags("Склад: за складами");
    }

    private static void MapPurchaseOrders(RouteGroupBuilder orders)
    {
        orders.MapGet("/", (IPurchaseOrderRepository repo, [AsParameters] PurchaseOrderFilter filter, CancellationToken ct) =>
            repo.ListAsync(filter, ct))
            .WithSummary("Замовлення постачальникам; status = draft, ordered, received або cancelled");

        orders.MapGet("/{id:int}", async (int id, IPurchaseOrderRepository repo, CancellationToken ct) =>
            await repo.GetDtoAsync(id, ct) is { } order ? Results.Ok(order) : Results.NotFound())
            .WithSummary("Замовлення з рядками, отриманим і готовим текстом для постачальника");

        orders.MapPost("/", async (CreatePurchaseOrderRequest request, ClaimsPrincipal user, PurchaseOrderService service, CancellationToken ct) =>
        {
            var id = await service.CreateAsync(request, user.GetUserId(), ct);
            return Results.Created($"/stock/purchase-orders/{id}", new { id });
        })
            .WithSummary("Створити чернетку вручну");

        orders.MapPost("/from-reorder/{supplierId:int}", async (int supplierId, ClaimsPrincipal user, PurchaseOrderService service, CancellationToken ct) =>
        {
            var id = await service.CreateFromReorderAsync(supplierId, user.GetUserId(), ct);
            return Results.Created($"/stock/purchase-orders/{id}", new { id });
        })
            .WithSummary("Створити чернетку для постачальника з того, що пропонує звіт «що дозамовити»");

        orders.MapPut("/{id:int}/lines", async (int id, SetPurchaseOrderLinesRequest request, PurchaseOrderService service, CancellationToken ct) =>
        {
            await service.SetLinesAsync(id, request, ct);
            return Results.NoContent();
        })
            .WithSummary("Змінити рядки чернетки");

        orders.MapPost("/{id:int}/ordered", async (int id, PurchaseOrderService service, CancellationToken ct) =>
        {
            await service.MarkOrderedAsync(id, ct);
            return Results.NoContent();
        })
            .WithSummary("Позначити, що замовлення надіслано постачальнику");

        orders.MapPost("/{id:int}/receive", async (int id, ReceivePurchaseOrderRequest request, ClaimsPrincipal user, PurchaseOrderService service, CancellationToken ct) =>
        {
            await service.ReceiveAsync(id, request, user.GetUserId(), user.GetUsername(), ct);
            return Results.NoContent();
        })
            .WithSummary("Прийняти товар (можна частинами): залишки зростають, у журналі складу — прихід");

        orders.MapPost("/{id:int}/cancel", async (int id, PurchaseOrderService service, CancellationToken ct) =>
        {
            await service.CancelAsync(id, ct);
            return Results.NoContent();
        });
    }
}