using NewErp.Api.Auth;
using NewErp.Application.Competitors;

namespace NewErp.Api.Endpoints;

public static class CompetitorEndpoints
{
    public static void MapCompetitorEndpoints(this IEndpointRouteBuilder app)
    {
        var competitors = app.MapGroup("/competitors").WithTags("Ціни конкурентів").RequireAuthorization(Policies.Orders);

        competitors.MapGet("/", (CompetitorService service, CancellationToken ct) => service.CompetitorsAsync(ct))
            .WithSummary("Конкуренти з кількістю сторінок наших товарів");

        competitors.MapPost("/", async (SaveCompetitorRequest request, CompetitorService service, CancellationToken ct) =>
        {
            var competitor = await service.CreateCompetitorAsync(request, ct);
            return Results.Created($"/competitors/{competitor.Id}", competitor);
        })
            .WithSummary("Додати конкурента: назва, сайт, необов'язковий вираз для ціни (перша група — ціна)");

        competitors.MapPut("/{id:int}", (int id, SaveCompetitorRequest request, CompetitorService service, CancellationToken ct) =>
            service.UpdateCompetitorAsync(id, request, ct))
            .WithSummary("Змінити конкурента; неактивного не перевіряє щоденна перевірка й не показує порівняння");

        competitors.MapGet("/links", (int? productId, CompetitorService service, CancellationToken ct) => service.LinksAsync(productId, ct))
            .WithSummary("Сторінки наших товарів у конкурентів з останньою ціною й помилкою перевірки");

        competitors.MapPost("/links", async (AddCompetitorLinkRequest request, CompetitorService service, CancellationToken ct) =>
        {
            var link = await service.AddLinkAsync(request, ct);
            return Results.Created($"/competitors/links/{link.Id}", link);
        })
            .WithSummary("Додати сторінку товару: адреса має бути на сайті конкурента, один товар — одна сторінка в конкурента");

        competitors.MapDelete("/links/{id:int}", async (int id, CompetitorService service, CancellationToken ct) =>
        {
            await service.DeleteLinkAsync(id, ct);
            return Results.NoContent();
        })
            .WithSummary("Видалити сторінку разом з історією цін");

        competitors.MapPost("/links/{id:int}/price", (int id, CompetitorPriceRequest request, CompetitorService service, CancellationToken ct) =>
            service.SetPriceAsync(id, request, ct))
            .WithSummary("Вписати ціну вручну (для сайту, який не дає її прочитати)");

        competitors.MapPost("/links/{id:int}/check", (int id, CompetitorService service, CancellationToken ct) => service.CheckAsync(id, ct))
            .WithSummary("Прочитати ціну з цієї сторінки зараз");

        competitors.MapGet("/links/{id:int}/history", (int id, CompetitorService service, CancellationToken ct) => service.HistoryAsync(id, ct))
            .WithSummary("Історія цін сторінки: рядок, коли ціна змінилась, з джерелом auto чи manual");

        competitors.MapPost("/check", (CompetitorService service, CancellationToken ct) => service.CheckAllAsync(ct))
            .WithSummary("Прочитати ціни всіх сторінок активних конкурентів (так само щодня робить фонова перевірка)");

        competitors.MapGet("/report", (CompetitorService service, CancellationToken ct) => service.ReportAsync(ct))
            .WithSummary("Наша ціна проти останніх цін активних конкурентів: найнижча й різниця у відсотках");
    }
}
