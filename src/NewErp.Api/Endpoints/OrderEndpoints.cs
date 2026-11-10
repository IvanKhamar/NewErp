using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Delivery;
using NewErp.Application.Orders;
using NewErp.Application.Sms;
using NewErp.Domain.Common;
using NewErp.Domain.Orders;

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

        orders.MapGet("/{id:int}/composition", (int id, NewOrderService service, CancellationToken ct) => service.GetCompositionAsync(id, ct))
            .WithSummary("Склад замовлення для редагування: товари з розмірами, доставка, знижка, завдаток");

        orders.MapPut("/{id:int}/composition", (int id, UpdateOrderCompositionRequest request, ClaimsPrincipal user, NewOrderService service, CancellationToken ct) =>
            service.UpdateCompositionAsync(id, request, user.GetUserId(), ct))
        .WithSummary("Замінити склад замовлення (товари, розміри, кількість, ціни, доставка, знижка, завдаток). "
            + "Пошиті вироби перераховуються за новим складом; warnings — нестача");

        orders.MapPut("/{id:int}/manager", async (int id, AssignManagerRequest request, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
        {
            await service.AssignManagerAsync(id, request, user.GetUserId(), ct);
            return Results.NoContent();
        })
        .WithSummary("Призначити відповідального менеджера");

        orders.MapPost("/{id:int}/sms", (int id, SendSmsRequest request, SmsService service, CancellationToken ct) =>
            service.SendToOrderAsync(id, request, ct))
        .WithSummary("Надіслати SMS клієнту замовлення (через SMSClub; без токена заглушка: нічого не надсилає, sent = false, але пише в журнал)");

        app.MapGet("/sms/settings", (ISmsSender sender) => new SmsSettingsDto(sender.IsConfigured))
            .WithTags("Замовлення").RequireAuthorization(Policies.Orders)
            .WithSummary("Чи підключено справжній SMS-сервіс (isConfigured = false — працює заглушка)");

        orders.MapGet("/{id:int}/sms", (int id, ISmsLog log, CancellationToken ct) => log.ListForOrderAsync(id, ct))
        .WithSummary("SMS, надіслані клієнту цього замовлення");

        MapOrderTools(orders);
    }

    /// <summary>Інструменти сторінки замовлення старої адмінки (пункти 35–42, перенесено з Python-версії).</summary>
    private static void MapOrderTools(RouteGroupBuilder orders)
    {
        orders.MapPost("/new/shipping-cost", (ShippingCostRequest request, ShipmentService shipments, CancellationToken ct) =>
            shipments.EstimateCostAsync(request, ct))
            .WithSummary("Вартість доставки Новою Поштою за вагою й розміром товарів");

        orders.MapGet("/discrepancies", (ShippingCheckService checks, CancellationToken ct, int days = ShippingChecks.DiscrepancyDays) =>
            checks.DiscrepanciesAsync(days, ct))
            .WithSummary("Розбіжності статусу й ТТН за останні дні");

        // Python будує сторінки друку на сервері; Blazor говорить лише з API, тож дані для друку — окремий ендпоінт.
        orders.MapGet("/print", (int[] ids, IOrderQueries queries, CancellationToken ct) =>
            ids.Length is > 0 and <= MaxPrintOrders
                ? queries.PrintAsync(ids, ct)
                : throw new DomainException($"Оберіть від 1 до {MaxPrintOrders} замовлень."))
            .WithSummary("Дані для друку рахунку й пакувального листа (ids=1&ids=2)");

        orders.MapGet("/{id:int}/shipping-check", (int id, int statusId, ShippingCheckService checks, CancellationToken ct) =>
            checks.CheckAsync(id, statusId, ct))
            .WithSummary("Чи можна поставити статус: ТТН і SMS перед «Відправлено»");

        orders.MapGet("/{id:int}/customer", (int id, NewOrderService service, CancellationToken ct) => service.GetCustomerAsync(id, ct))
            .WithSummary("Покупець, адреса й способи доставки та оплати для редагування");

        orders.MapPut("/{id:int}/customer", async (int id, UpdateOrderCustomerRequest request, ClaimsPrincipal user, NewOrderService service, CancellationToken ct) =>
        {
            await service.UpdateCustomerAsync(id, request, user.GetUserId(), ct);
            return Results.NoContent();
        })
        .WithSummary("Змінити покупця, адресу, способи доставки й оплати");

        orders.MapPut("/{id:int}/shipping-date", async (int id, ShippingDateRequest request, OrderService service, CancellationToken ct) =>
        {
            await service.SetShippingDateAsync(id, request, ct);
            return Results.NoContent();
        })
        .WithSummary("Планова дата відправки (null — прибрати)");

        orders.MapPost("/{id:int}/invoice", (int id, OrderService service, CancellationToken ct) => service.CreateInvoiceAsync(id, ct))
            .WithSummary("Номер рахунку: новий наступний або вже наданий");

        orders.MapGet("/{id:int}/credits", (int id, OrderCreditService credits, CancellationToken ct) => credits.GetAsync(id, ct))
            .WithSummary("Бонусні бали клієнта й комісія партнера за замовлення");

        orders.MapPost("/{id:int}/reward", (int id, ClaimsPrincipal user, OrderCreditService credits, CancellationToken ct) =>
            credits.AddRewardAsync(id, user.GetUserId(), ct))
            .WithSummary("Нарахувати клієнту бонусні бали замовлення (повторно — нічого)");

        orders.MapDelete("/{id:int}/reward", (int id, ClaimsPrincipal user, OrderCreditService credits, CancellationToken ct) =>
            credits.RemoveRewardAsync(id, user.GetUserId(), ct))
            .WithSummary("Зняти нараховані бонусні бали");

        orders.MapPost("/{id:int}/commission", (int id, ClaimsPrincipal user, OrderCreditService credits, CancellationToken ct) =>
            credits.AddCommissionAsync(id, user.GetUserId(), ct))
            .WithSummary("Нарахувати партнерові комісію замовлення (повторно — нічого)");

        orders.MapDelete("/{id:int}/commission", (int id, ClaimsPrincipal user, OrderCreditService credits, CancellationToken ct) =>
            credits.RemoveCommissionAsync(id, user.GetUserId(), ct))
            .WithSummary("Зняти нараховану комісію");

        orders.MapDelete("/{id:int}", (int id, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
            service.DeleteAsync(id, user.GetUserId(), ct))
            .RequireAuthorization(Policies.OrderDelete)
            .WithSummary("Видалити замовлення без ТТН, чеку, оплати й повернень (лише адміністратор); warnings — від модулів");
    }

    private const int MaxPrintOrders = 100;
}