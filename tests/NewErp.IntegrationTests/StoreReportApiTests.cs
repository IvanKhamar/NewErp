using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Reports;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>Звіт UTM, дані ремаркетингу замовлення, звіти про вітрину й стандартні звіти OpenCart. Окрема база: тести додають свої дані.</summary>
public class StoreReportApiTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const int CustomerId = 1;

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        if (await db.CustomerSearches.AnyAsync()) // екземпляр класу новий на кожен тест, а база спільна
            return;

        // Модуль пише 0000-00-00 00:00:00 для події, яку не надіслав; зберегти таке може лише сеанс без суворих дат.
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("SET SESSION sql_mode = ''");
        foreach (var (orderId, source, medium, campaign, ga4) in new[]
        {
            (ApiFactory.OrderId, "ig", "paid", "autumn", "2026-10-01 12:05:00"), // 1500
            (2, "google", "cpc", "brand", "0000-00-00 00:00:00"), // 2000
            (ApiFactory.CategoryOrderId, "ig", "paid", "summer", "0000-00-00 00:00:00"), // вересень
        })
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO dd_remarketing_orders (order_id, first_referrer, utm_source, utm_medium, utm_campaign, ga4) VALUES ({orderId}, 'https://instagram.com/', {source}, {medium}, {campaign}, {ga4})");
        }

        db.DdCustomers.Add(new DdCustomer
        {
            CustomerId = CustomerId, CustomerGroupId = 1, LanguageId = ApiFactory.LanguageId, Firstname = "Олена", Lastname = "Коваль", Email = "olena@example.com",
            Telephone = "0930000000", Fax = "", Password = "", Salt = "", CustomField = "", Ip = "", Status = true, Safe = false, Token = "", Code = "", AddressId = 0,
            DateAdded = new DateTime(2024, 5, 1),
        });
        db.CustomersOnline.AddRange(
            new CustomerOnline { Ip = "192.0.2.10", CustomerId = CustomerId, Url = "https://shop.test/sukni", Referer = "https://instagram.com/", DateAdded = new DateTime(2026, 11, 2, 10, 0, 0) },
            new CustomerOnline { Ip = "192.0.2.11", CustomerId = 0, Url = "https://shop.test/x", Referer = "", DateAdded = new DateTime(2026, 11, 2, 10, 5, 0) });
        db.CustomerRewards.AddRange(
            new CustomerReward { CustomerId = CustomerId, OrderId = ApiFactory.OrderId, Description = "За замовлення", Points = 300, DateAdded = new DateTime(2026, 10, 5, 12, 0, 0) },
            new CustomerReward { CustomerId = CustomerId, OrderId = ApiFactory.OrderId, Description = "Списано в замовленні", Points = -100, DateAdded = new DateTime(2026, 10, 5, 12, 0, 0) });
        await db.DdProducts.Where(p => p.ProductId == ApiFactory.CategoryProductId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Viewed, 40));
        db.DdOrderTotals.AddRange(
            new DdOrderTotal { OrderId = ApiFactory.OrderId, Code = "tax", Title = "ПДВ 20%", Value = 250, SortOrder = 5 },
            new DdOrderTotal { OrderId = 2, Code = "tax", Title = "ПДВ 20%", Value = 333.33m, SortOrder = 5 });
        db.CustomerSearches.AddRange(
            new CustomerSearch { Keyword = "льон", Products = 4, DateAdded = new DateTime(2026, 10, 2, 10, 0, 0), Ip = "10.0.0.1", LanguageId = ApiFactory.LanguageId },
            new CustomerSearch { Keyword = "льон", Products = 2, DateAdded = new DateTime(2026, 10, 3, 10, 0, 0), Ip = "10.0.0.1", LanguageId = ApiFactory.LanguageId });
        db.StoreStatistics.Add(new StoreStatistic { Code = "order_sale", Value = 12345 });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [MySqlFact]
    public async Task Utm_ByPeriod_Source_AndFilters()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        const string url = "/reports/utm?from=2026-09-01&to=2026-10-31&group=Month";

        var report = (await client.GetFromJsonAsync<UtmReportDto>(url))!;
        Assert.Equal(["google", "ig"], report.Sources);
        var october = Assert.Single(report.Rows, r => r.Start == new DateOnly(2026, 10, 1));
        Assert.Equal(2, october.Orders);
        Assert.Null(october.UtmSource);

        var bySource = (await client.GetFromJsonAsync<UtmReportDto>(url + "&bySource=true"))!.Rows;
        Assert.Equal(["google", "ig"], bySource.Where(r => r.Start == new DateOnly(2026, 10, 1)).Select(r => r.UtmSource));

        Assert.Equal(1, (await client.GetFromJsonAsync<UtmReportDto>(url + "&source=ig&campaign=autumn"))!.Orders);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/reports/utm?from=2026-10-31&to=2026-09-01")).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/reports/utm")).StatusCode);
    }

    [MySqlFact]
    public async Task OrderRemarketing_ShowsFilledTags_AndSentEventsOnly()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var json = await client.GetStringAsync($"/orders/{ApiFactory.OrderId}/remarketing");
        var data = JsonSerializer.Deserialize<JsonElement>(json);
        var utm = data.GetProperty("utm"); // ключі — назви колонок, як у Python
        Assert.Equal("ig", utm.GetProperty("utm_source").GetString());
        Assert.Equal("https://instagram.com/", utm.GetProperty("first_referrer").GetString());
        Assert.False(utm.TryGetProperty("utm_term", out _)); // порожні не показуємо
        var pixel = Assert.Single(data.GetProperty("events").EnumerateArray());
        Assert.Equal("Google Analytics 4", pixel.GetProperty("name").GetString());
        Assert.Equal("2026-10-01T12:05:00", pixel.GetProperty("sentAt").GetString());

        Assert.Empty((await client.GetFromJsonAsync<OrderRemarketingDto>("/orders/2/remarketing"))!.Events);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/orders/999/remarketing")).StatusCode);
    }

    [MySqlFact]
    public async Task StorefrontReports()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var online = (await client.GetFromJsonAsync<List<OnlineVisitorDto>>("/reports/online"))!;
        Assert.Equal([("192.0.2.11", null), ("192.0.2.10", "Олена Коваль")], online.Select(v => (v.Ip, v.Customer)));

        var viewed = Assert.Single((await client.GetFromJsonAsync<List<ProductViewsDto>>("/reports/product-views"))!);
        Assert.Equal((ApiFactory.CategoryProductId, 40), (viewed.ProductId, viewed.Viewed));
        Assert.Equal(Math.Round(viewed.Sold * 100m / 40, 2), viewed.Percent);

        var reward = Assert.Single((await client.GetFromJsonAsync<List<CustomerRewardDto>>("/reports/rewards"))!);
        Assert.Equal(("Олена Коваль", 200), (reward.Customer, reward.Points));

        var taxes = (await client.GetFromJsonAsync<TaxReportDto>("/reports/taxes?from=2026-10-01&to=2026-10-31&group=Month"))!;
        var row = Assert.Single(taxes.Rows);
        Assert.Equal(("ПДВ 20%", 2, 583.33m), (row.Title, row.Orders, row.Total));

        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/reports/online")).StatusCode);
    }

    [MySqlTheory]
    [InlineData("sales-orders")]
    [InlineData("sales-returns")]
    [InlineData("sales-shipping")]
    [InlineData("sales-coupons")]
    [InlineData("products-purchased")]
    [InlineData("customer-orders")]
    [InlineData("customer-transactions")]
    [InlineData("customer-activity")]
    [InlineData("customer-searches")]
    [InlineData("statistics")]
    public async Task EveryStandardReport_Runs(string kind)
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        foreach (var group in new[] { "day", "week", "month" })
        {
            var response = await client.GetAsync($"/reports/standard/{kind}?from=2026-09-01&to=2026-11-30&group={group}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var report = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
            var columns = report.GetProperty("columns").GetArrayLength();
            Assert.True(columns >= 2);
            Assert.All(report.GetProperty("rows").EnumerateArray(), r => Assert.Equal(columns, r.GetArrayLength()));
        }
    }

    [MySqlFact]
    public async Task StandardReports_Numbers_AndLimits()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        JsonElement Rows(string json) => JsonSerializer.Deserialize<JsonElement>(json).GetProperty("rows");

        var orders = Rows(await client.GetStringAsync("/reports/standard/sales-orders?from=2026-10-01&to=2026-10-31&group=month"))[0];
        Assert.Equal("2026-10-01", orders[0].GetString());
        Assert.True(orders[1].GetInt32() >= 2);

        var searches = Rows(await client.GetStringAsync("/reports/standard/customer-searches?from=2026-10-01&to=2026-10-31"));
        Assert.Equal("""[["льон",2,4,"2026-10-03"]]""", searches.GetRawText());
        Assert.Equal("""[["order_sale",12345]]""", Rows(await client.GetStringAsync("/reports/standard/statistics")).GetRawText());

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/reports/standard/sales-orders?from=2026-10-31&to=2026-10-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/reports/standard/sales-orders?from=2024-01-01&to=2026-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/reports/standard/everything")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/reports/standard/sales-orders?group=year")).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/reports/standard/statistics")).StatusCode);
    }
}
