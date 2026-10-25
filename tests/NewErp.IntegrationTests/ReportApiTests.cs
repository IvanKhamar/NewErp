using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Reports;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

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

    [MySqlFact]
    public async Task Statuses_CountCancelledButNotUnfinishedOrders_AndReturnsOfThePeriod()
    {
        await SeedReturnsAsync();
        var manager = await factory.LoginAsync(ApiFactory.Manager);

        var report = await manager.GetFromJsonAsync<StatusReportDto>("/reports/statuses?from=2025-01-01&to=2026-10-31");
        Assert.Equal(
            [(ApiFactory.Cancelled, "Відмінено", 1, 700m), (ApiFactory.Completed, "5) Відправлено", 1, 2000m), (ApiFactory.Pending, "1) Уточнюємо", 3, 3600m)],
            report!.Orders.Select(r => (r.StatusId, r.Name, r.Orders, r.Total)));
        Assert.Equal((5, 6300m), (report.OrderCount, report.Total));
        // Статус 3 є лише в поверненні, без назви в довіднику: показується номером. Повернення 2024 року — поза періодом.
        Assert.Equal([(1, "Очікує", 2, 3), (3, "#3", 1, 1)], report.Returns.Select(r => (r.StatusId, r.Name, r.Returns, r.Quantity)));
        Assert.Equal(3, report.ReturnCount);

        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/reports/statuses?from=2026-10-05&to=2026-10-01")).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/reports/statuses")).StatusCode);
    }

    private async Task SeedReturnsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.DdReturnStatuses.Add(new DdReturnStatus { ReturnStatusId = 1, LanguageId = ApiFactory.LanguageId, Name = "Очікує" });
        db.DdReturns.AddRange(
            Return(ApiFactory.OrderId, 1, 1, new DateTime(2026, 10, 10, 12, 0, 0)),
            Return(2, 1, 2, new DateTime(2026, 10, 12, 12, 0, 0)),
            Return(ApiFactory.CategoryOrderId, 3, 1, new DateTime(2026, 9, 5, 12, 0, 0)),
            Return(ApiFactory.CancelledOrderId, 1, 5, new DateTime(2024, 3, 5, 12, 0, 0)));
        await db.SaveChangesAsync();
    }

    private static DdReturn Return(int orderId, int statusId, int quantity, DateTime added) => new()
    {
        OrderId = orderId, ProductId = ApiFactory.ProductId, Firstname = "Ірина", Lastname = "Шевчук", Email = "", Telephone = "",
        Product = "Сукня", Model = "T-1", Quantity = quantity, ReturnStatusId = statusId, DateOrdered = added.Date, DateAdded = added,
        DateModified = added,
    };
}
