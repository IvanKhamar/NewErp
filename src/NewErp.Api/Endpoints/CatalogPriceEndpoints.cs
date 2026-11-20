using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Catalog;

namespace NewErp.Api.Endpoints;

/// <summary>Масові ціни (пункт 46) і фільтри розмірів (пункт 52): лише адміністратор.</summary>
public static class CatalogPriceEndpoints
{
    public static void MapCatalogPriceEndpoints(this WebApplication app)
    {
        var prices = app.MapGroup("/catalog/prices").WithTags("Масові ціни").RequireAuthorization(Policies.CatalogBulk);

        prices.MapPost("/preview", (PriceChangeRequest request, PriceChangeService service, CancellationToken ct) =>
                service.PreviewAsync(request, ct))
            .WithSummary("Що зміниться: ціни чи відсоток акції товарів обраних категорій і виробників");

        prices.MapPost("/apply", (PriceChangeRequest request, ClaimsPrincipal user, PriceChangeService service, CancellationToken ct) =>
                service.ApplyAsync(request, user.GetUserId(), ct))
            .WithSummary("Змінити ціни чи акції (кожна зміна — в журнал аудиту)");

        prices.MapPost("/size-filters", (ClaimsPrincipal user, SizeFilterService service, CancellationToken ct) =>
                service.RebuildAsync(user.GetUserId(), ct))
            .WithSummary("Фільтри розмірів за розмірами товарів (додати бракуючі, зняти зайві)");
    }
}
