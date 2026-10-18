using NewErp.Api.Auth;
using NewErp.Application.Partners;

namespace NewErp.Api.Endpoints;

public static class PartnerEndpoints
{
    public static void MapPartnerEndpoints(this IEndpointRouteBuilder app)
    {
        var partners = app.MapGroup("/partners").WithTags("Партнери").RequireAuthorization(Policies.Orders);

        partners.MapGet("/", (IPartnerRepository repo, CancellationToken ct) => repo.ListAsync(ct))
            .WithSummary("Партнери-перепродавці з кількістю рядків відповідності");

        partners.MapGet("/{id:int}", async (int id, IPartnerRepository repo, CancellationToken ct) =>
            await repo.GetDtoAsync(id, ct) is { } partner ? Results.Ok(partner) : Results.NotFound())
            .WithSummary("Партнер з відповідністю категорій і переліком окремих товарів");

        partners.MapPost("/", async (SavePartnerRequest request, PartnerService service, CancellationToken ct) =>
        {
            var id = await service.CreateAsync(request, ct);
            return Results.Created($"/partners/{id}", new { id });
        });

        partners.MapPut("/{id:int}", async (int id, SavePartnerRequest request, PartnerService service, CancellationToken ct) =>
        {
            await service.UpdateAsync(id, request, ct);
            return Results.NoContent();
        })
            .WithSummary("Змінити партнера разом з відповідностями (рядки з тією самою категорією чи товаром і розміром зберігають свій id)");

        partners.MapDelete("/{id:int}", async (int id, PartnerService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        partners.MapGet("/categories", (IPartnerRepository repo, CancellationToken ct) => repo.ListCategoriesAsync(ct))
            .WithSummary("Категорії магазину з розмірами їхніх товарів");

        partners.MapGet("/products", (string? search, PartnerService service, CancellationToken ct) => service.SearchProductsAsync(search, ct))
            .WithSummary("Пошук товарів за назвою чи id, з цінами розмірів");

        // Фід забирає сайт партнера, тож він без входу, як і на старому сайті. Вимкнений фід — 404.
        app.MapGet("/feeds/{code}.xml", async (string code, PartnerService service, CancellationToken ct) =>
            await service.BuildFeedXmlAsync(code, ct) is { } xml
                ? Results.Text(xml, "application/xml; charset=utf-8")
                : Results.NotFound())
            .AllowAnonymous()
            .WithTags("Партнери")
            .WithSummary("XML-фід партнера (yml_catalog) з його категоріями й цінами");
    }
}
