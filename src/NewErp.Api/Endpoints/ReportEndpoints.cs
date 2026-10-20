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
    }
}
