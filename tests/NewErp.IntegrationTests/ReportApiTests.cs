using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Reports;

namespace NewErp.IntegrationTests;

/// <summary>Звіти на замовленнях із ApiFactory. Окрема тестова база, щоб інші тести не додали замовлень.</summary>
public class ReportApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task Sales_ByMonth_WithoutCancelledAndUnfinished_ProductsAndDashboard()
    {
        var manager = await factory.LoginAsync(ApiFactory.Manager);

        var sales = await manager.GetFromJsonAsync<SalesReportDto>("/reports/sales?from=2025-01-01&to=2026-10-31&group=Month");
        Assert.Equal(
            [(new DateOnly(2026, 8, 1), 1200m), (new DateOnly(2026, 9, 1), 900m), (new DateOnly(2026, 10, 1), 3500m)],
            sales!.Rows.Select(r => (r.Start, r.Total)));
        Assert.Equal(4, sales.Orders);

        var cancelled = await manager.GetFromJsonAsync<SalesReportDto>($"/reports/sales?from=2025-01-01&to=2026-10-31&statusId={ApiFactory.Cancelled}");
        Assert.Equal((1, 700m), (cancelled!.Orders, cancelled.Total));

        var products = await manager.GetFromJsonAsync<ProductSalesDto>("/reports/products?from=2026-10-01&to=2026-10-31");
        Assert.Equal((ApiFactory.ProductId, 2), (products!.Rows[0].ProductId, products.Rows[0].Quantity));

        var dashboard = await manager.GetFromJsonAsync<DashboardDto>("/reports/dashboard");
        Assert.Equal(30, dashboard!.Last30Days.Count);

        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/reports/sales?from=2026-10-05&to=2026-10-01")).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/reports/dashboard")).StatusCode);
    }
}
