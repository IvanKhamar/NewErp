using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Orders;
using NewErp.Application.Reports;
using NewErp.Web.Api;
using NewErp.Web.Pages.Reports;

namespace NewErp.Web.Tests;

public class ReportPagesTests : WebTestContext
{
    [Fact]
    public void Dashboard_ShowsTilesWithChange_ChartAndTopProducts()
    {
        Services.AddScoped<ReportsApi>();
        var days = Enumerable.Range(0, 30)
            .Select(i => new SalesReportRow(new DateOnly(2026, 9, 21).AddDays(i), new DateOnly(2026, 9, 21).AddDays(i), i % 3, i % 3, i * 100m))
            .ToList();
        Api.Respond("/reports/dashboard", HttpStatusCode.OK, new DashboardDto(
            new(3, 2, 50), new(4500m, 3000m, 50), new(40, 50, -20), new(60000m, 50000m, 20), new(1500m, 1000m, 50), new(5, 0, null),
            days, [new(101, "Сукня лляна", "SK-01", 12, 18000m)]));

        var panel = Render<DashboardPanel>();

        panel.WaitForAssertion(() => Assert.Contains("Сукня лляна", panel.Find("[data-test=top]").TextContent));
        var tiles = panel.Find("[data-test=tiles]").TextContent;
        Assert.Contains("+50% проти вчора", tiles);
        Assert.Contains("-20% проти ті самі дні минулого місяця", tiles);
        Assert.Contains("минулий місяць: 0", tiles);
        Assert.Equal(30, panel.FindAll("[data-test=chart] > *").Count);
    }

    [Fact]
    public void Reports_ShowSalesByPeriodWithTotals_AndProducts()
    {
        Services.AddScoped<ReportsApi>();
        Services.AddScoped<OrdersApi>();
        Api.Respond("/orders/statuses", HttpStatusCode.OK, new List<OrderStatusDto> { new(22, "5) Відправлено", "#00ff00") });
        Api.Respond("/reports/sales", HttpStatusCode.OK, new SalesReportDto(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20),
            [new(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 4), 2, 3, 2500m), new(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 11), 1, 1, 900m)],
            3, 4, 3400m));
        Api.Respond("/reports/products", HttpStatusCode.OK, new ProductSalesDto(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20),
            [new(101, "Сукня лляна", "SK-01", 3, 4500m)]));
        Api.Respond("/reports/statuses", HttpStatusCode.OK, new StatusReportDto(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20),
            [new(22, "5) Відправлено", 3, 3400m)], 3, 3400m, [], 0));

        var page = Render<Reports>();

        page.WaitForAssertion(() => Assert.Contains("01.10.2026 – 04.10.2026", page.Find("[data-test=sales]").TextContent));
        Assert.Contains("Разом", page.Find("[data-test=sales]").TextContent);
        Assert.Contains("/reports/products", Api.Requests);
        Assert.Contains("/reports/statuses", Api.Requests);
    }
}
