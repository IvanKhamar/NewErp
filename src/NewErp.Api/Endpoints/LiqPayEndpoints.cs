using NewErp.Api.Auth;
using NewErp.Application.Payments;

namespace NewErp.Api.Endpoints;

/// <summary>Оплата замовлення карткою через LiqPay: посилання для клієнта й перевірка оплати.</summary>
public static class LiqPayEndpoints
{
    public static void MapLiqPayEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders/{id:int}").WithTags("Оплати").RequireAuthorization(Policies.Finance);

        orders.MapPost("/liqpay-link", (int id, CreatePaymentLinkRequest request, LiqPayService service, CancellationToken ct) =>
                service.CreateLinkAsync(id, request, ct))
            .WithSummary("Посилання на оплату LiqPay (amount не задано — уся сума замовлення); без ключів LiqPay посилання тестове: isTest = true");

        orders.MapGet("/liqpay-status", (int id, LiqPayService service, CancellationToken ct) => service.StatusAsync(id, ct))
            .WithSummary("Чи оплачено замовлення через LiqPay (запит status за номером замовлення)");
    }
}
