using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Application.Common;
using NewErp.Application.Payments;
using NewErp.Application.Sms;
using NewErp.Application.Stock;
using NewErp.Domain.Payments;
using NewErp.Infrastructure.Persistence;

namespace NewErp.IntegrationTests;

/// <summary>SMS про мало на складі за журналом рухів і автоматичне зіставлення платежів.</summary>
public class LowStockAndAutoMatchApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed class FakeSms : ISmsSender
    {
        public List<(string Phone, string Text)> Sent { get; } = [];

        public Task<SmsSendResult> SendAsync(string phone, string text, CancellationToken ct)
        {
            Sent.Add((phone, text));
            return Task.FromResult(new SmsSendResult(true, "ok"));
        }
    }

    private sealed record Created(int Id);

    private static async Task<int> CreateItemAsync(HttpClient client, string name, string unit, decimal minimum)
    {
        var response = await client.PostAsJsonAsync("/stock/items", new SaveStockItemRequest(null, null, name, unit, minimum));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private static async Task AdjustAsync(HttpClient client, int item, decimal delta, string reason) =>
        (await client.PostAsJsonAsync($"/stock/items/{item}/adjustments", new AdjustStockRequest(delta, reason))).EnsureSuccessStatusCode();

    [MySqlFact]
    public async Task LowStockAlert_NamesOnlyItemsThatFellBelow_SinceYesterday()
    {
        var sms = new FakeSms();
        WebApplicationFactory<Program> app = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Stock:AlertPhones:0", "+380 (67) 000-00-01");
            b.UseSetting("Stock:AlertPhones:1", "0670000002");
            b.ConfigureTestServices(s => s.AddSingleton<ISmsSender>(sms));
        });
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.Storekeeper, ApiFactory.Password))).EnsureSuccessStatusCode();
        var fell = await CreateItemAsync(client, "Льон для сповіщення", "м", 10);
        var alwaysLow = await CreateItemAsync(client, "Ґудзики давно мало", "шт", 50);
        await AdjustAsync(client, fell, 15, "Прихід");
        await AdjustAsync(client, fell, -9, "Пошиття"); // 15 → 6: перетнули мінімум
        await AdjustAsync(client, alwaysLow, 5, "Прихід"); // 0 → 5: не перетнули

        var result = await (await client.PostAsync("/stock/alerts/low-stock", null)).Content.ReadFromJsonAsync<LowStockAlertResult>();

        // База спільна для тестів цього класу, тож інші позиції теж можуть бути в списку: важливо, кого в ньому немає.
        Assert.Equal((2, 2), (result!.Sent, result.Phones));
        Assert.Equal("380670000001", sms.Sent[0].Phone);
        Assert.Contains("Льон для сповіщення 6 м (мін. 10)", sms.Sent[0].Text);
        Assert.DoesNotContain("Ґудзики", sms.Sent[0].Text);

        var manager = await factory.LoginAsync(ApiFactory.Manager);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync("/stock/alerts/low-stock", null)).StatusCode);
    }

    [MySqlFact]
    public async Task LowStockAlert_WithoutPhones_SendsNothing()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);
        var item = await CreateItemAsync(client, "Нитки без телефонів", "шт", 10);
        await AdjustAsync(client, item, 15, "Прихід");
        await AdjustAsync(client, item, -9, "Пошиття");

        var result = await (await client.PostAsync("/stock/alerts/low-stock", null)).Content.ReadFromJsonAsync<LowStockAlertResult>();

        Assert.Equal(0, result!.Sent);
        Assert.Equal(0, result.Phones);
        Assert.True(result.Items >= 1);
    }

    [MySqlFact]
    public async Task AutoMatch_LinksOlderPayments_WithOneConfidentOrder()
    {
        // 2000 з телефоном замовлення 2 у призначенні — упевнено. Записаний у базі платіж (прізвище й сума) — ні.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            db.BankPayments.Add(new BankPayment("tx-phone", "UA00", 2000m, 7000m, "Оплата від 0670000002", "", new DateOnly(2026, 10, 4), new TimeOnly(11, 0)));
            await db.SaveChangesAsync();
        }
        var client = await factory.LoginAsync(ApiFactory.Manager);
        const string url = "/payments/bank/auto-match?from=2026-10-01&to=2026-10-31";

        var result = (await (await client.PostAsync(url, null)).Content.ReadFromJsonAsync<AutoMatchResult>())!;

        Assert.Equal(2, result.Checked);
        Assert.Equal(2, Assert.Single(result.Links).OrderId);
        var stillOpen = (await client.GetFromJsonAsync<PagedResult<BankPaymentDto>>("/payments/bank?linked=false"))!;
        Assert.Equal(ApiFactory.BankAppCode, Assert.Single(stillOpen.Items).AppCode);

        // Тепер замовлення 2 оплачене, тож записаний платіж не був би впевненим навіть із його телефоном.
        Assert.Empty((await (await client.PostAsync(url, null)).Content.ReadFromJsonAsync<AutoMatchResult>())!.Links);

        var bad = await client.PostAsync("/payments/bank/auto-match?from=2026-10-31&to=2026-10-01", null);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.PostAsync("/payments/bank/auto-match", null)).StatusCode);
    }
}
