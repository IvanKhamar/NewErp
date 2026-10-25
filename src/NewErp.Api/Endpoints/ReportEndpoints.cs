using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

using NewErp.Api.Auth;
using NewErp.Application.Reports;

namespace NewErp.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var reports = app.MapGroup("/reports").WithTags("Звіти").RequireAuthorization(Policies.Orders);

        reports.MapGet("/dashboard", (ReportService service, CancellationToken ct) => service.DashboardAsync(ct))
            .WithSummary("Зведення для головної: замовлення й продажі сьогодні та за місяць проти попередніх, середній чек, нові покупці, графік за 30 днів, топ-5 товарів");

        reports.MapGet("/sales", ([AsParameters] ReportFilter filter, ReportService service, CancellationToken ct) => service.SalesAsync(filter, ct))
            .WithSummary("Продажі за періодами (group: Day, Week, Month, Year); типово — поточний місяць без скасованих замовлень");

        reports.MapGet("/products", ([AsParameters] ReportFilter filter, ReportService service, CancellationToken ct) => service.ProductsAsync(filter, ct))
            .WithSummary("Продані товари за кількістю (до 200); типово — поточний місяць без скасованих замовлень");

        reports.MapGet("/statuses", ([AsParameters] ReportFilter filter, ReportService service, CancellationToken ct) => service.StatusesAsync(filter, ct))
            .WithSummary("Замовлення (кількість і сума) та повернення за статусами, скасовані теж; типово — поточний місяць");

        // Фід забирає зовнішній дашборд, тож він без входу, але з токеном. Без токена в налаштуваннях — 404, з чужим — 401.
        app.MapGet("/feeds/revenue.json", async (
                [Microsoft.AspNetCore.Mvc.FromHeader(Name = "X-Feed-Token")] string? header, string? token, int? days,
                IOptions<RevenueFeedOptions> options, ReportService service, CancellationToken ct) =>
            {
                var feed = options.Value;
                if (!feed.Enabled)
                    return Results.NotFound();
                if (!Matches(header ?? token, feed.Token))
                    return Results.Unauthorized();
                return Results.Ok(await service.RevenueFeedAsync(days ?? 30, feed.MaxDays, ct));
            })
            .AllowAnonymous()
            .WithTags("Звіти")
            .WithSummary("Виручка для зовнішнього дашборда: сьогодні, вчора, місяць проти тих самих днів минулого, рік і ряд за days днів. "
                + "Токен — у заголовку X-Feed-Token або ?token=");
    }

    /// <summary>Порівняння за сталий час, щоб час відповіді не підказував, яка частина токена вгадана.</summary>
    private static bool Matches(string? given, string expected) =>
        given is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given.Trim()), Encoding.UTF8.GetBytes(expected.Trim()));
}
