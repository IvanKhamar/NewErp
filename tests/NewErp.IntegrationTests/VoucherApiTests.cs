using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Common;
using NewErp.Application.Marketing;
using NewErp.Application.Sms;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>
/// Подарункові сертифікати, як VoucherControllerTest у Java-версії. Замовлення OrderId ще не оплачене, замовлення 2 — у статусі
/// «5) Відправлено», який рахується оплаченим. Без SMS-ключа заглушка нічого не надсилає. Сертифікати вигадані.
/// </summary>
public class VoucherApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int PaidOrderId = 2;

    [MySqlFact]
    public async Task List_AndCard_WithBalanceUsabilityAndHistory()
    {
        await SeedAsync();
        var admin = await factory.LoginAsync(ApiFactory.Admin);

        var list = (await admin.GetFromJsonAsync<List<VoucherCardDto>>("/marketing/vouchers"))!;
        Assert.Equal([(4, "OFF200", false), (3, "SHOP700", true), (2, "SHOP500", false), (1, "GIFT1000", true)],
            list.Select(v => (v.Id, v.Code, v.Usable)));
        Assert.Equal(650m, list[3].Balance);
        Assert.Equal([1], (await admin.GetFromJsonAsync<List<VoucherCardDto>>("/marketing/vouchers?search=олена"))!.Select(v => v.Id));

        var card = (await admin.GetFromJsonAsync<VoucherCardDto>("/marketing/vouchers/1"))!;
        Assert.Equal(("GIFT1000", "Свято", 1000m, 650m), (card.Code, card.ThemeName, card.Amount, card.Balance));
        var spent = Assert.Single(card.History);
        Assert.Equal((ApiFactory.OrderId, $"Тарас Клієнт{ApiFactory.OrderId}", -350m), (spent.OrderId, spent.Customer, spent.Amount));

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/marketing/vouchers/99")).StatusCode);
        Assert.Equal([new LookupItem(1, "Свято")], (await admin.GetFromJsonAsync<List<LookupItem>>("/marketing/vouchers/themes"))!);
    }

    [MySqlFact]
    public async Task Create_GeneratesACode_AndRefusesDuplicatesAndBadAmounts()
    {
        await SeedAsync();
        var admin = await factory.LoginAsync(ApiFactory.Admin);

        var created = await admin.PostAsJsonAsync("/marketing/vouchers", new { toName = "Софія", amount = 1500 });
        var voucher = (await created.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<VoucherCardDto>())!;
        Assert.Matches("^[A-HJ-NP-Z2-9]{8}$", voucher.Code);
        Assert.Equal((0, 1500m, true, true), (voucher.OrderId, voucher.Balance, voucher.Active, voucher.Usable));

        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/marketing/vouchers", new { code = "gift1000", toName = "Софія", amount = 100 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/marketing/vouchers", new { toName = "Софія", amount = 0 })).StatusCode);

        // Більша сума сертифіката не чіпає історію: залишок іде за сумою.
        var updated = await admin.PutAsJsonAsync("/marketing/vouchers/1", new { code = "GIFT1000", toName = "Олена", amount = 1200 });
        Assert.Equal(850m, (await updated.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<VoucherCardDto>())!.Balance);
    }

    [MySqlFact]
    public async Task OnlyAnUnspentVoucherIsDeleted_AndOnlyAUsableOneIsTexted()
    {
        await SeedAsync();
        var admin = await factory.LoginAsync(ApiFactory.Admin);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync("/marketing/vouchers/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/marketing/vouchers/4")).StatusCode);

        var sent = await admin.PostAsJsonAsync("/marketing/vouchers/1/sms", new SendVoucherSmsRequest("+38 (067) 111-22-33"));
        Assert.False((await sent.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<SmsSendResult>())!.Sent); // заглушка
        using (var scope = factory.Services.CreateScope())
        {
            var sms = await scope.ServiceProvider.GetRequiredService<ErpDbContext>().SmsMessages.AsNoTracking().SingleAsync();
            Assert.Equal("380671112233", sms.Phone);
            Assert.Contains("650", sms.Text, StringComparison.Ordinal);
            Assert.Contains("GIFT1000", sms.Text, StringComparison.Ordinal);
        }

        Assert.Equal(HttpStatusCode.BadRequest, // ще не оплачений
            (await admin.PostAsJsonAsync("/marketing/vouchers/2/sms", new SendVoucherSmsRequest("380671112233"))).StatusCode);
    }

    [MySqlFact]
    public async Task Storekeeper_HasNoAccess()
    {
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/marketing/vouchers")).StatusCode);
    }

    // Як vouchers.sql у Java: кожен тест починає з тих самих сертифікатів і порожнього журналу SMS.
    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.DdVouchers.ExecuteDeleteAsync();
        await db.DdVoucherHistories.ExecuteDeleteAsync();
        await db.DdVoucherThemes.ExecuteDeleteAsync();
        await db.DdVoucherThemeDescriptions.ExecuteDeleteAsync();
        await db.SmsMessages.ExecuteDeleteAsync();

        db.DdVoucherThemes.Add(new DdVoucherTheme { VoucherThemeId = 1, Image = "catalog/demo/canon_eos_5d_2.jpg" });
        db.DdVoucherThemeDescriptions.AddRange(
            new DdVoucherThemeDescription { VoucherThemeId = 1, LanguageId = ApiFactory.LanguageId, Name = "Свято" },
            new DdVoucherThemeDescription { VoucherThemeId = 1, LanguageId = 1, Name = "Holiday" });
        db.DdVouchers.AddRange(
            // створений в адмінці, частково витрачений у замовленні OrderId
            Voucher(1, 0, "GIFT1000", "Магазин", "Олена", 1, 1000m, true, new DateTime(2026, 9, 1, 10, 0, 0), "З днем народження!"),
            // куплений у замовленні OrderId, яке ще не оплачене
            Voucher(2, ApiFactory.OrderId, "SHOP500", "Ірина", "Марія", 0, 500m, true, new DateTime(2026, 10, 1, 10, 0, 0)),
            // куплений в оплаченому замовленні
            Voucher(3, PaidOrderId, "SHOP700", "Петро", "Андрій", 0, 700m, true, new DateTime(2026, 10, 5, 12, 0, 0)),
            Voucher(4, 0, "OFF200", "Магазин", "Тарас", 0, 200m, false, new DateTime(2026, 8, 1, 10, 0, 0)));
        db.DdVoucherHistories.Add(new DdVoucherHistory
        {
            VoucherHistoryId = 1, VoucherId = 1, OrderId = ApiFactory.OrderId, Amount = -350m, DateAdded = new DateTime(2026, 10, 1, 10, 0, 0),
        });
        await db.SaveChangesAsync();
    }

    private static DdVoucher Voucher(int id, int orderId, string code, string from, string to, int themeId, decimal amount, bool active,
        DateTime added, string message = "") => new()
    {
        VoucherId = id, OrderId = orderId, Code = code, FromName = from, FromEmail = "", ToName = to, ToEmail = "", VoucherThemeId = themeId,
        Message = message, Amount = amount, Status = active, DateAdded = added,
    };
}
