using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Marketing;
using NewErp.Domain.Sms;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>Іменинники, купон з SMS і вивантаження контактів. Окрема тестова база, бо тест змінює замовлення й купони.</summary>
public class MarketingApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Phone = "380670000002"; // телефон замовлення 2 у спільних тестових даних

    [MySqlFact]
    public async Task Birthday_GreetingWithCoupon_IsLoggedAndCounted()
    {
        await SetBirthdayTodayAsync();
        var manager = await factory.LoginAsync(ApiFactory.Manager);

        var contact = Assert.Single((await manager.GetFromJsonAsync<List<BirthdayContactDto>>("/marketing/birthdays?period=Today"))!);
        Assert.Equal((Phone, "Тарас", 0), (contact.Phone, contact.FirstName, contact.SentThisYear));

        var code = (await (await manager.PostAsync("/marketing/birthdays/coupon-code", null)).Content.ReadFromJsonAsync<CouponCodeDto>())!.Code;
        var sent = await manager.PostAsJsonAsync("/marketing/birthdays/sms", new BirthdaySmsRequest(contact.Phone, $"Вітаємо! Промокод {code}", code));
        var result = (await sent.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<BirthdaySmsResult>())!;
        Assert.False(result.Sent); // у тестах SMS іде в заглушку
        Assert.NotNull(result.CouponId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var coupon = await db.DdCoupons.AsNoTracking().SingleAsync(c => c.Code == code);
            Assert.Equal(("P", 20m, 1, true), (coupon.Type, coupon.Discount, coupon.UsesTotal, coupon.Status));
            var sms = await db.SmsMessages.AsNoTracking().SingleAsync(m => m.PreOrderId == SmsMessage.BirthdayMarker);
            Assert.Equal((0, Phone), (sms.OrderId, sms.Phone));
        }

        Assert.Equal(1, (await manager.GetFromJsonAsync<List<BirthdayContactDto>>("/marketing/birthdays?period=Week"))!.Single().SentThisYear);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await manager.PostAsJsonAsync("/marketing/birthdays/sms", new BirthdaySmsRequest(Phone, "Ще раз", code))).StatusCode); // купон уже є

        var orders = (await manager.GetFromJsonAsync<List<CustomerOrderDto>>("/marketing/customers/orders?phone=0670000002"))!;
        Assert.Equal((2, "5) Відправлено"), (Assert.Single(orders).OrderId, orders[0].Status));

        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/marketing/birthdays")).StatusCode);
    }

    [MySqlFact]
    public async Task Export_MergesSources_AndFillsNamesFromOrders()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            db.DdQuestions.Add(new DdQuestion { Name = "Гість", Telephone = "+38 (067) 000-00-02", Question = "Є розмір M?", DateAdded = new DateTime(2026, 10, 5) });
            db.DdQuestions.Add(new DdQuestion { Name = "Гість", Telephone = "093 555 11 22", Question = "?", DateAdded = new DateTime(2026, 10, 6) });
            db.DdQuestions.Add(new DdQuestion { Name = "Давно", Telephone = "0931234567", Question = "?", DateAdded = new DateTime(2025, 1, 1) });
            db.DdSubscribes.Add(new DdSubscribe { Email = "news@test.ua", DateAdded = new DateTime(2026, 10, 5) });
            await db.SaveChangesAsync();
        }
        var manager = await factory.LoginAsync(ApiFactory.Manager);

        var response = await manager.GetAsync("/marketing/contacts.csv?field=Phone&sources=Orders&sources=Questions&from=2026-10-01&to=2026-10-31");
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        var lines = (await response.Content.ReadAsStringAsync()).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Ім'я;Прізвище;Місто;Телефон;E-mail", lines[0]);
        Assert.Contains("Тарас;Клієнт2;;380670000002;", lines);
        Assert.Contains(";;;380935551122;", lines);
        Assert.DoesNotContain(lines, l => l.Contains("380931234567", StringComparison.Ordinal)); // поза періодом

        var emails = await manager.GetStringAsync("/marketing/contacts.csv?field=Email&sources=Subscribers");
        Assert.Contains(";;;news@test.ua;", emails);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/marketing/contacts.csv?field=Email")).StatusCode);
    }

    private async Task SetBirthdayTodayAsync()
    {
        var today = DateTime.Now;
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ErpDbContext>().DdOrders.Where(o => o.OrderId == 2)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.BirthdayDay, today.Day).SetProperty(o => o.BirthdayMonth, today.Month));
    }
}
