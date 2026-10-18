using NewErp.Api.Auth;
using NewErp.Application.Catalog;

namespace NewErp.Api.Endpoints;

public static class PreOrderEndpoints
{
    public static void MapPreOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var preOrders = app.MapGroup("/preorders").WithTags("Передзамовлення").RequireAuthorization(Policies.Orders);

        preOrders.MapGet("/", (string? search, bool? onlyWithQuota, PreOrderService service, CancellationToken ct) =>
            service.ListAsync(new PreOrderFilter(search, onlyWithQuota ?? false), ct))
            .WithSummary("Розміри товарів (до 200) з залишком, лімітом передзамовлення, очікуваною датою й тим, як їх продає вітрина");

        preOrders.MapPut("/{productOptionValueId:int}", async (int productOptionValueId, UpdatePreOrderRequest request, PreOrderService service, CancellationToken ct) =>
        {
            await service.UpdateAsync(productOptionValueId, request, ct);
            return Results.NoContent();
        })
            .WithSummary("Ліміт передзамовлення розміру (0 — не продавати наперед) і дата, коли розмір очікується");

        preOrders.MapPost("/settle", (PreOrderService service, CancellationToken ct) => service.SettleAsync(ct))
            .WithSummary("Списати продане наперед (мінусовий залишок) з лімітів; фонова задача робить це сама що SettleIntervalMinutes");
    }
}
