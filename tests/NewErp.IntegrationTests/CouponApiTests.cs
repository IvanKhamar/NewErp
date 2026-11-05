using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Marketing;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>Купони зі списками «діє» й «не діє», історією і старими нульовими датами.</summary>
public class CouponApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/marketing/coupons";

    private static SaveCouponRequest Body(string code = "autumn10") => new(
        "Осінній розпродаж", code, "P", 10, 1000, DateStart: new DateOnly(2026, 11, 1), DateEnd: new DateOnly(2026, 11, 30), UsesTotal: 0, UsesCustomer: 1,
        CategoryIds: [ApiFactory.CategoryId], ExcludedProductIds: [ApiFactory.CategoryProductId]);

    private async Task WithDbAsync(Func<ErpDbContext, Task> work)
    {
        using var scope = factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    private Task AddCategoryNameAsync() => WithDbAsync(async db =>
    {
        if (await db.DdCategoryDescriptions.AnyAsync(d => d.CategoryId == ApiFactory.CategoryId && d.LanguageId == ApiFactory.LanguageId))
            return;
        db.DdCategoryDescriptions.Add(new DdCategoryDescription
        {
            CategoryId = ApiFactory.CategoryId, LanguageId = ApiFactory.LanguageId, Name = "Сорочки", Description = "", MetaTitle = "", MetaDescription = "", MetaKeyword = "",
        });
        await db.SaveChangesAsync();
    });

    [MySqlFact]
    public async Task Admin_WithExclusions_HistoryAndDelete()
    {
        await AddCategoryNameAsync();
        var client = await factory.LoginAsync(ApiFactory.Manager);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Url, Body() with { ExcludedProductIds = [999999] })).StatusCode);
        var created = await client.PostAsJsonAsync(Url, Body());
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var card = (await created.Content.ReadFromJsonAsync<CouponCardDto>())!;
        Assert.Equal(("AUTUMN10", "Сорочки"), (card.Code, card.Categories[0].Name));
        Assert.Equal([ApiFactory.CategoryProductId], card.ExcludedProducts.Select(p => p.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Url, Body())).StatusCode); // код зайнятий

        var changed = await (await client.PutAsJsonAsync($"{Url}/{card.Id}", Body() with
        {
            ProductIds = [ApiFactory.CategoryProductId],
            ExcludedProductIds = [],
        })).Content.ReadFromJsonAsync<CouponCardDto>();
        Assert.Equal([ApiFactory.CategoryProductId], changed!.Products.Select(p => p.Id));
        Assert.Empty(changed.ExcludedProducts);

        var listed = Assert.Single((await client.GetFromJsonAsync<List<CouponListItem>>($"{Url}?search=autumn"))!);
        Assert.Equal((0, new DateOnly(2026, 11, 30)), (listed.Used, listed.DateEnd));

        await WithDbAsync(async db =>
        {
            db.CouponHistories.Add(new CouponHistory { CouponId = card.Id, OrderId = ApiFactory.OrderId, Amount = -150, DateAdded = new DateTime(2026, 11, 2, 10, 0, 0) });
            await db.SaveChangesAsync();
        });
        var refused = await client.DeleteAsync($"{Url}/{card.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("Купон уже використали в замовленнях — його можна лише вимкнути.", (await refused.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        Assert.Equal(ApiFactory.OrderId, (await client.GetFromJsonAsync<CouponCardDto>($"{Url}/{card.Id}"))!.History[0].OrderId);

        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync(Url)).StatusCode);
    }

    [MySqlFact]
    public async Task AnUnusedCoupon_IsDeletedWithItsLists_AndAMissingOneIs404()
    {
        await AddCategoryNameAsync();
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var card = (await (await client.PostAsJsonAsync(Url, Body("gone1"))).Content.ReadFromJsonAsync<CouponCardDto>())!;

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Url}/{card.Id}")).StatusCode);

        await WithDbAsync(async db => Assert.False(await db.CouponProductExcludes.AnyAsync(x => x.CouponId == card.Id)));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{card.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{Url}/{card.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{Url}/{card.Id}", Body("gone2"))).StatusCode);
    }

    [MySqlFact]
    public async Task AnOldCoupon_WithZeroDates_CanBeRead_AndSaved()
    {
        var id = 0;
        await WithDbAsync(async db =>
        {
            // Старий сайт пише 0000-00-00; такий рядок може зберегти лише сеанс без суворих дат.
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("SET SESSION sql_mode = ''");
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO dd_coupon (name, code, type, discount, logged, shipping, total, date_start, date_end, uses_total, uses_customer, status, date_added) " +
                "VALUES ('Старий', 'OLDZERO1', 'P', 5, 0, 0, 0, '0000-00-00', '0000-00-00', 1, 'x', 1, '2020-01-01')");
            id = await db.DdCoupons.Where(c => c.Code == "OLDZERO1").Select(c => c.CouponId).FirstAsync();
        });
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var card = await client.GetFromJsonAsync<CouponCardDto>($"{Url}/{id}");
        Assert.Equal((null, null, 0), (card!.DateStart, card.DateEnd, card.UsesCustomer));
        var listed = Assert.Single((await client.GetFromJsonAsync<List<CouponListItem>>($"{Url}?search=OLDZERO1"))!);
        Assert.Null(listed.DateStart);

        var saved = await client.PutAsJsonAsync($"{Url}/{id}", new SaveCouponRequest(
            "Старий", "OLDZERO1", "P", 5, 0, DateStart: new DateOnly(2026, 1, 1), DateEnd: new DateOnly(2026, 2, 1)));
        saved.EnsureSuccessStatusCode();
        Assert.Equal(new DateOnly(2026, 2, 1), (await saved.Content.ReadFromJsonAsync<CouponCardDto>())!.DateEnd);
    }
}
