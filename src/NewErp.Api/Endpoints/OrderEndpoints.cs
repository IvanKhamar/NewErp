using NewErp.Application.Orders;

namespace NewErp.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders").WithTags("Замовлення");

        // [AsParameters] збирає параметри рядка запиту (?statusId=5&page=2) в один record OrderFilter.
        orders.MapGet("/", (IOrderQueries queries, [AsParameters] OrderFilter filter, CancellationToken ct) =>
            queries.ListAsync(filter, ct))
            .WithSummary("Список замовлень з фільтрами і пагінацією");

        orders.MapGet("/{id:int}", async (int id, IOrderQueries queries, CancellationToken ct) =>
            await queries.GetAsync(id, ct) is { } order ? Results.Ok(order) : Results.NotFound())
            .WithSummary("Картка замовлення: товари та історія статусів");

        orders.MapGet("/statuses", (IOrderQueries queries, CancellationToken ct) => queries.ListStatusesAsync(ct))
            .WithSummary("Довідник статусів");

        orders.MapGet("/sources", (IOrderQueries queries, CancellationToken ct) => queries.ListSourcesAsync(ct))
            .WithSummary("Довідник джерел замовлення");

        orders.MapPut("/{id:int}/status", async (int id, ChangeOrderStatusRequest request, OrderService service, CancellationToken ct) =>
        {
            await service.ChangeStatusAsync(id, request, ct);
            return Results.NoContent();
        })
        .WithSummary("Змінити статус (із записом у журнал)");

        orders.MapPut("/{id:int}/manager", async (int id, AssignManagerRequest request, OrderService service, CancellationToken ct) =>
        {
            await service.AssignManagerAsync(id, request, ct);
            return Results.NoContent();
        })
        .WithSummary("Призначити відповідального менеджера");
    }
}