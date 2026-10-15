using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Orders;
using NewErp.Application.Sms;

namespace NewErp.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders").WithTags("Замовлення").RequireAuthorization(Policies.Orders);

        // [AsParameters] збирає параметри рядка запиту (?statusId=5&page=2) в один record OrderFilter.
        orders.MapGet("/", (IOrderQueries queries, [AsParameters] OrderFilter filter, CancellationToken ct) =>
            queries.ListAsync(filter, ct))
            .WithSummary("Список замовлень з фільтрами і пагінацією");

        orders.MapGet("/{id:int}", async (int id, IOrderQueries queries, CancellationToken ct) =>
            await queries.GetAsync(id, ct) is { } order ? Results.Ok(order) : Results.NotFound())
            .WithSummary("Картка замовлення: товари та історія статусів");

        orders.MapGet("/statuses", (IOrderQueries queries, CancellationToken ct) => queries.ListStatusesAsync(ct))
            .WithSummary("Довідник статусів");

        orders.MapGet("/managers", (IOrderQueries queries, CancellationToken ct) => queries.ListManagersAsync(ct))
            .WithSummary("Кого можна призначити відповідальним (активні користувачі)");

        orders.MapGet("/sources", (IOrderQueries queries, CancellationToken ct) => queries.ListSourcesAsync(ct))
            .WithSummary("Довідник джерел замовлення");

        orders.MapPut("/{id:int}/status", (int id, ChangeOrderStatusRequest request, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
            service.ChangeStatusAsync(id, request, user.GetUserId(), ct))
        .WithSummary("Змінити статус (із записом у журнал). warnings — попередження від інших модулів");

        orders.MapPut("/{id:int}/manager", async (int id, AssignManagerRequest request, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
        {
            await service.AssignManagerAsync(id, request, user.GetUserId(), ct);
            return Results.NoContent();
        })
        .WithSummary("Призначити відповідального менеджера");

        orders.MapPost("/{id:int}/sms", (int id, SendSmsRequest request, SmsService service, CancellationToken ct) =>
            service.SendToOrderAsync(id, request, ct))
        .WithSummary("Надіслати SMS клієнту замовлення (зараз заглушка: нічого не надсилає, sent = false, але пише в журнал)");

        orders.MapGet("/{id:int}/sms", (int id, ISmsLog log, CancellationToken ct) => log.ListForOrderAsync(id, ct))
        .WithSummary("SMS, надіслані клієнту цього замовлення");
    }
}