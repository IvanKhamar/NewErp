using System.Security.Claims;

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

        var shipment = app.MapGroup("/orders/{id:int}/shipment").WithTags("Доставка").RequireAuthorization(Policies.Orders);

        shipment.MapGet("/form", (int id, ShipmentService service, CancellationToken ct) => service.FormAsync(id, ct))
            .WithSummary("Форма нової ТТН, заповнена даними замовлення (місто й відділення — з довідника, якщо знайшлися)");

        shipment.MapPost("/", (int id, CreateShipmentRequest request, ShipmentService service, ClaimsPrincipal user, CancellationToken ct) =>
                service.CreateAsync(id, request, user.GetUserId(), ct))
            .WithSummary("Створити ТТН у Новій Пошті (без ключа API — лише тестову, якщо NovaPoshta:FakeDocuments)");

        shipment.MapDelete("/", async (int id, ShipmentService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        })
            .WithSummary("Видалити ТТН у Новій Пошті й у нас");

        // PDF віддаємо без імені файлу, щоб браузер відкрив його для друку, а не завантажив.
        shipment.MapGet("/print", async (int id, ShipmentService service, CancellationToken ct, ShipmentPrintKind kind = ShipmentPrintKind.Document) =>
        {
            var (pdf, _) = await service.PrintAsync(id, kind, ct);
            return Results.File(pdf, "application/pdf");
        })
            .WithSummary("PDF накладної (kind=Document) чи маркування (kind=Markings); ключ API лишається на сервері");

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

        delivery.MapGet("/cities", (string? search, ShipmentService service, CancellationToken ct) => service.SearchCitiesAsync(search, ct))
            .WithSummary("Міста з довідника, назва яких починається з search");

        delivery.MapGet("/warehouses", (string cityRef, string? search, ShipmentService service, CancellationToken ct) =>
                service.SearchWarehousesAsync(cityRef, search, ct))
            .WithSummary("Робочі відділення й поштомати міста, у назві яких є search");

        delivery.MapPost("/warehouses/refresh", (ShipmentService service, CancellationToken ct) => service.RefreshWarehousesAsync(ct))
            .WithSummary("Завантажити свіжий довідник відділень з Нової Пошти (спільний з вітриною старого сайту)");
    }
}
