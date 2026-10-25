using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Delivery;

namespace NewErp.Api.Endpoints;

/// <summary>
/// Накладні за кордон (Nova Post International) поруч із внутрішніми: ТТН замовлення одна, хоч якого перевізника, її читають і
/// видаляють тим самим /orders/{id}/shipment. Той самий контракт, що в Java-версії (InternationalShipmentController).
/// </summary>
public static class InternationalShipmentEndpoints
{
    public static void MapInternationalShipmentEndpoints(this IEndpointRouteBuilder app)
    {
        var shipment = app.MapGroup("/orders/{id:int}/shipment/international").WithTags("Доставка").RequireAuthorization(Policies.Orders);

        shipment.MapGet("/form", (int id, InternationalShipmentService service, CancellationToken ct) => service.FormAsync(id, ct))
            .WithSummary("Форма міжнародної накладної з даними замовлення; коди УКТЗЕД, англійські назви й вагу вводять вручну");

        shipment.MapPost("/", (int id, CreateInternationalShipmentRequest request, InternationalShipmentService service, ClaimsPrincipal user,
                CancellationToken ct) => service.CreateAsync(id, request, user.GetUserId(), ct))
            .WithSummary("Створити накладну в Nova Post International (без ключа API — лише тестову, якщо NovaPostInternational:FakeDocuments)");

        var delivery = app.MapGroup("/delivery/international").WithTags("Доставка").RequireAuthorization(Policies.Orders);

        delivery.MapGet("/countries", () => InternationalShipmentService.Countries())
            .WithSummary("Країни отримувача (усі, крім України), за українською назвою");

        delivery.MapGet("/hs-codes", (string? search, string? country, InternationalShipmentService service, CancellationToken ct) =>
                service.HsCodesAsync(search, country, ct))
            .WithSummary("Підказки кодів УКТЗЕД за словом (від двох літер) для країни отримувача");
    }
}
