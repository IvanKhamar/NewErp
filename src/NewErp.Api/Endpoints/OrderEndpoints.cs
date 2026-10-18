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

        orders.MapPost("/", async (CreateOrderRequest request, ClaimsPrincipal user, NewOrderService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, user.GetUserId(), ct);
            return Results.Created($"/orders/{result.OrderId}", result);
        })
        .WithSummary("Створити замовлення вручну (дзвінок, соцмережі). risk — чи скасовував клієнт попередні замовлення");

        orders.MapGet("/new/methods", (NewOrderService service, CancellationToken ct) => service.ListMethodsAsync(ct))
            .WithSummary("Способи доставки й оплати, які вже траплялись у замовленнях");

        orders.MapGet("/new/products", (string? search, NewOrderService service, CancellationToken ct) => service.SearchProductsAsync(search, ct))
            .WithSummary("Пошук товару за назвою, моделлю чи id: ціна й розміри для нового замовлення");

        orders.MapGet("/new/customers", (string? search, NewOrderService service, CancellationToken ct) => service.SearchCustomersAsync(search, ct))
            .WithSummary("Клієнти з попередніх замовлень (від 3 символів імені, телефону чи email)");

        orders.MapGet("/customer-risk", ([AsParameters] CustomerRiskQuery query, NewOrderService service, CancellationToken ct) =>
            service.CheckRiskAsync(query, ct))
            .WithSummary("Чи скасовував клієнт замовлення за останні 4 роки (за телефоном, ім'ям і прізвищем або email)");

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