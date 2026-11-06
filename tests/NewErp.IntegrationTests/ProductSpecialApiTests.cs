using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Orders;
using NewErp.Infrastructure.Persistence;

namespace NewErp.IntegrationTests;

/// <summary>Акції (dd_product_special) у пошуку товарів нового замовлення.</summary>
public class ProductSpecialApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Search = "/orders/new/products?search=лляна";

    private async Task WithDbAsync(Func<ErpDbContext, Task> work)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.ProductSpecials.ExecuteDeleteAsync();
        await work(db);
    }

    [MySqlFact]
    public async Task Search_GivesTheFirstActiveSpecial_AndKeepsTheRegularPrice()
    {
        var today = DateOnly.FromDateTime(ApiFactory.BusinessNow);
        ProductSpecial Special(int priority, decimal price, decimal percent, DateOnly start, DateOnly end) => new()
        {
            ProductId = ApiFactory.CategoryProductId, CustomerGroupId = 1, Priority = priority, Price = price, Percent = percent,
            DateStart = start, DateEnd = end,
        };
        await WithDbAsync(async db =>
        {
            db.ProductSpecials.AddRange(
                Special(1, 500, 0, today.AddDays(-30), today), // закінчилася сьогодні: день кінця вже не діє
                Special(1, 650, 0, today.AddDays(1), today.AddDays(9)), // ще не почалася
                Special(2, 0, 15, today, today.AddDays(7)), // почалася сьогодні: діє
                Special(3, 700, 0, today.AddDays(-1), today.AddDays(7))); // нижчий пріоритет
            await db.SaveChangesAsync();
        });
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var product = Assert.Single((await client.GetFromJsonAsync<List<ProductPickDto>>(Search))!);

        Assert.Equal((770m, 900m), (product.Price, product.RegularPrice));
    }

    [MySqlFact]
    public async Task ASpecialWithoutDates_IsAlwaysOn()
    {
        await WithDbAsync(async db =>
        {
            // Старий сайт пише 0000-00-00 як «без межі»; такий рядок може зберегти лише сеанс без суворих дат.
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("SET SESSION sql_mode = ''");
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO dd_product_special (product_id, customer_group_id, price, date_start, date_end) VALUES ({ApiFactory.CategoryProductId}, 1, 799, '0000-00-00', '0000-00-00')");
        });
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var product = Assert.Single((await client.GetFromJsonAsync<List<ProductPickDto>>(Search))!);

        Assert.Equal((799m, 900m), (product.Price, product.RegularPrice));
    }

    [MySqlFact]
    public async Task WithoutASpecial_ThereIsNoRegularPrice()
    {
        await WithDbAsync(_ => Task.CompletedTask);
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var product = Assert.Single((await client.GetFromJsonAsync<List<ProductPickDto>>(Search))!);

        Assert.Equal((900m, null), (product.Price, product.RegularPrice));
    }
}
