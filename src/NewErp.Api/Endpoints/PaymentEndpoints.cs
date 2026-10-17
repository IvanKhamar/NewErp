using NewErp.Api.Auth;
using NewErp.Application.Payments;

namespace NewErp.Api.Endpoints;

/// <summary>Надходження на рахунки ПриватБанку та зіставлення їх із замовленнями.</summary>
public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var payments = app.MapGroup("/payments/bank").WithTags("Оплати").RequireAuthorization(Policies.Finance);

        payments.MapGet("/", ([AsParameters] BankPaymentFilter filter, BankPaymentService service, CancellationToken ct) =>
                service.ListAsync(filter, ct))
            .WithSummary("Надходження з фільтрами (період, linked, замовлення, пошук, hidden), посторінково");

        payments.MapPost("/import", (DateOnly? from, DateOnly? to, BankPaymentService service, CancellationToken ct) =>
                service.ImportAsync(from, to, ct))
            .WithSummary("Завантажити виписку (типово вчора й сьогодні); без ключів банку нічого не запитує: clientConfigured = false");

        payments.MapGet("/{id:int}/suggestions", (int id, string? customer, BankPaymentService service, CancellationToken ct) =>
                service.SuggestAsync(id, customer, ct))
            .WithSummary("Замовлення, яким імовірно належить платіж (за сумою, номером і телефоном у призначенні), найкращі першими");

        payments.MapPut("/{id:int}/order", (int id, LinkPaymentRequest request, BankPaymentService service, CancellationToken ct) =>
                service.LinkAsync(id, request, ct))
            .WithSummary("Прив'язати платіж до замовлення (orderId = 0 — відв'язати)");

        payments.MapPut("/{id:int}/visible", async (int id, bool visible, BankPaymentService service, CancellationToken ct) =>
        {
            await service.SetVisibilityAsync(id, visible, ct);
            return Results.NoContent();
        }).WithSummary("Приховати платіж (visible=false), що не стосується замовлень, або повернути його");

        app.MapGet("/orders/{id:int}/payments", (int id, BankPaymentService service, CancellationToken ct) => service.ForOrderAsync(id, ct))
            .WithTags("Оплати").RequireAuthorization(Policies.Finance)
            .WithSummary("Банківські надходження, прив'язані до замовлення");
    }
}
