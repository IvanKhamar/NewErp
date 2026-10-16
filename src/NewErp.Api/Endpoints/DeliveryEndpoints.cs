using NewErp.Api.Auth;
using NewErp.Application.Delivery;

namespace NewErp.Api.Endpoints;

/// <summary>Доставка Новою Поштою: ТТН замовлення, трекінг, нагадування. Доступ — як до замовлень (Admin, Manager).</summary>
public static class DeliveryEndpoints
{
    public static void MapDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/orders/{id:int}/shipment", async (int id, IShipmentRepository repo, CancellationToken ct) =>
            await repo.GetForOrderAsync(id, ct) is { } shipment ? Results.Ok(shipment) : Results.NoContent())
            .WithTags("Доставка").RequireAuthorization(Policies.Orders)
            .WithSummary("ТТН замовлення зі статусом (204 — ТТН ще немає)");

        var delivery = app.MapGroup("/delivery").WithTags("Доставка").RequireAuthorization(Policies.Orders);

        delivery.MapPost("/tracking/refresh", (DeliveryService service, CancellationToken ct) => service.RefreshTrackingAsync(ct))
            .WithSummary("Оновити статуси незакритих ТТН у Нової Пошти (без ключа API нічого не запитує: clientConfigured = false)");

        delivery.MapGet("/reminders", (DeliveryService service, CancellationToken ct) => service.RemindersAsync(ct))
            .WithSummary("Посилки на відділенні, у яких за 0–3 дні почнеться платне зберігання, а клієнту ще не нагадували");

        delivery.MapPost("/reminders/{orderId:int}/done", async (int orderId, DeliveryService service, CancellationToken ct) =>
        {
            await service.MarkRemindedAsync(orderId, ct);
            return Results.NoContent();
        })
            .WithSummary("Позначити, що клієнту нагадали (зникне зі списку)");
    }
}
