using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Orders;
using NewErp.Infrastructure.Persistence;

namespace NewErp.IntegrationTests;

/// <summary>
/// Інструменти сторінки замовлення старої адмінки (пункти 35–42, перенесено з тестів Python-версії test_order_tools.py):
/// перевірка відправки й розбіжності, редагування покупця, дата відправки, рахунок і друк, бали й комісія, видалення,
/// вартість доставки Новою Поштою.
/// </summary>
public class OrderToolsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string NovaPoshta = "novaposhta_warehouse.novaposhta_warehouse";
    private const string Pickup = "pickup.pickup";
    private const int Shipped = 22, Awaiting = 21;

    private async Task<int> NewOrderAsync(HttpClient client, string phone, string shippingCode = NovaPoshta, int quantity = 1)
    {
        var request = new CreateOrderRequest(
            "Ольга", "Перевірка", phone, "", "", 1, ApiFactory.Pending, null,
            shippingCode, shippingCode == NovaPoshta ? "Нова Пошта" : "Самовивіз", "Київ", "", "Відділення №1", "", "",
            "cod", "Накладений платіж",
            [new NewOrderLineRequest(ApiFactory.CategoryProductId, 502, quantity, 900m)]);
        var response = await client.PostAsJsonAsync("/orders", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateOrderResult>())!.OrderId;
    }

    private async Task WithDbAsync(Func<ErpDbContext, Task> work)
    {
        using var scope = factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    private Task WaybillAsync(int orderId, string number, DateTime? created = null) => WithDbAsync(db =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dd_novaposhta_document (order_id, carrier, ref, number, cost, status, status_code, recipient_address, redelivery, date_created)
            VALUES ({orderId}, 'novaposhta', {"ref-" + number}, {number}, 70, '', 1, 'Київ', 0, {created ?? ApiFactory.BusinessNow})
            """));

    private Task SmsAsync(string phone, string text) => WithDbAsync(db =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dd_sms_log (order_id, pre_order_id, phone, text, response, date)
            VALUES (0, 0, {phone}, {text}, '', {ApiFactory.BusinessNow})
            """));

    private Task SqlAsync(FormattableString sql) => WithDbAsync(db => db.Database.ExecuteSqlInterpolatedAsync(sql));

    private static async Task<List<(string Level, string Text)>> IssuesAsync(HttpClient client, int orderId, int statusId)
    {
        var check = await client.GetFromJsonAsync<ShippingCheckDto>($"/orders/{orderId}/shipping-check?statusId={statusId}");
        return check!.Issues.Select(i => (i.Level, i.Text)).ToList();
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail;

    [MySqlFact]
    public async Task Shipped_NeedsAWaybill_AndItsSmsToTheOrderPhone()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var orderId = await NewOrderAsync(client, "+380671110001");

        Assert.Equal([("danger", "ТТН для цього замовлення не створена")], await IssuesAsync(client, orderId, Shipped));
        await WaybillAsync(orderId, "20450000001111");
        Assert.Equal([("warning", "SMS з номером ТТН клієнту не надсилалась")], await IssuesAsync(client, orderId, Shipped));
        await SmsAsync("380509999999", "Ваша ТТН 2045 0000 0011 11"); // пробіли в тексті не ховають номер
        var (level, text) = Assert.Single(await IssuesAsync(client, orderId, Shipped));
        Assert.Equal("danger", level);
        Assert.Contains("380509999999", text);
        await SmsAsync("0671110001", "ТТН 20450000001111"); // ті самі останні 9 цифр, що й +380671110001
        Assert.Empty(await IssuesAsync(client, orderId, Shipped));

        var pickup = await NewOrderAsync(client, "+380671110002", Pickup);
        Assert.Empty(await IssuesAsync(client, pickup, Shipped)); // самовивозу ТТН не потрібна
        Assert.Empty(await IssuesAsync(client, orderId, ApiFactory.Pending)); // інші статуси не перевіряються
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/orders/999999/shipping-check?statusId={Shipped}")).StatusCode);
    }

    [MySqlFact]
    public async Task AwaitingShipment_IsOnlyForPickups()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var pickup = await NewOrderAsync(client, "+380671110003", Pickup);
        Assert.Empty(await IssuesAsync(client, pickup, Awaiting));

        var novaPoshta = await NewOrderAsync(client, "+380671110004");
        Assert.Equal("warning", Assert.Single(await IssuesAsync(client, novaPoshta, Awaiting)).Level); // чи справді самовивіз?
        await WaybillAsync(pickup, "20450000002222");
        var (level, text) = Assert.Single(await IssuesAsync(client, pickup, Awaiting));
        Assert.Equal("danger", level);
        Assert.Contains("ТТН", text);
    }

    [MySqlFact]
    public async Task Discrepancies_ListStaleWaybills_AndShippedProblems()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var stale = await NewOrderAsync(client, "+380671110005");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/orders/{stale}/status", new ChangeOrderStatusRequest(ApiFactory.Accepted))).StatusCode);
        await WaybillAsync(stale, "20450000003333", ApiFactory.BusinessNow.AddHours(-5));
        var fresh = await NewOrderAsync(client, "+380671110006");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/orders/{fresh}/status", new ChangeOrderStatusRequest(ApiFactory.Accepted))).StatusCode);
        await WaybillAsync(fresh, "20450000004444"); // щойно створена: ще нормально
        var shipped = await NewOrderAsync(client, "+380671110007");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/orders/{shipped}/status", new ChangeOrderStatusRequest(Shipped))).StatusCode);

        var groups = (await client.GetFromJsonAsync<List<DiscrepancyGroupDto>>("/orders/discrepancies?days=3"))!;
        Assert.Equal(3, groups.Count);
        var staleIds = groups[0].Items.Select(i => i.OrderId).ToList();
        Assert.Contains(stale, staleIds);
        Assert.DoesNotContain(fresh, staleIds);
        Assert.Equal("danger", groups[1].Items.First(i => i.OrderId == shipped).Level);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/orders/discrepancies?days=500")).StatusCode); // обрізається до 30 днів
    }

    [MySqlFact]
    public async Task CustomerData_CanBeEdited_WithTheRulesOfANewOrder()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var orderId = await NewOrderAsync(client, "+380671110008");
        var customer = (await client.GetFromJsonAsync<OrderCustomerDto>($"/orders/{orderId}/customer"))!;
        Assert.Equal(("Ольга", "Київ", NovaPoshta), (customer.Firstname, customer.City, customer.ShippingCode));

        var changed = new UpdateOrderCustomerRequest(customer.Firstname, "Нова", customer.Telephone, customer.Email, customer.Instagram,
            customer.ShippingCode, customer.ShippingMethod, "Львів", customer.Address1, customer.Address2, "5", "12",
            customer.PaymentCode, customer.PaymentMethod, "Дзвонити");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/orders/{orderId}/customer", changed)).StatusCode);
        var order = (await client.GetFromJsonAsync<OrderDetails>($"/orders/{orderId}"))!;
        Assert.Equal(("Ольга Нова", "Львів", "Дзвонити"), (order.Customer, order.ShippingCity, order.Comment));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/orders/{orderId}/customer", changed with { ShippingMethod = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/orders/{orderId}/customer", changed with { Firstname = new string('я', 33) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/orders/999999/customer", changed)).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync($"/orders/{orderId}/customer")).StatusCode);
    }

    [MySqlFact]
    public async Task ShippingDate_IsSavedAndCleared_AndAZeroDateReadsAsEmpty()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var orderId = await NewOrderAsync(client, "+380671110009");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/orders/{orderId}/shipping-date", new ShippingDateRequest(new DateOnly(2026, 11, 20)))).StatusCode);
        Assert.Equal(new DateOnly(2026, 11, 20), (await client.GetFromJsonAsync<OrderDetails>($"/orders/{orderId}"))!.DateShipping);
        Assert.Contains("\"dateShipping\":\"2026-11-20\"", await client.GetStringAsync($"/orders/{orderId}"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/orders/{orderId}/shipping-date", new ShippingDateRequest(null))).StatusCode);
        Assert.Null((await client.GetFromJsonAsync<OrderDetails>($"/orders/{orderId}"))!.DateShipping);

        await WithDbAsync(async db =>
        {
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"SET SESSION sql_mode = ''"); // старий сайт записував нульові дати
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dd_order SET date_shipping = '0000-00-00' WHERE order_id = {orderId}");
        });
        Assert.Null((await client.GetFromJsonAsync<OrderDetails>($"/orders/{orderId}"))!.DateShipping);
    }

    [MySqlFact]
    public async Task InvoiceNumbers_FollowTheirPrefix_AndAreGivenOnce()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var first = await NewOrderAsync(client, "+380671110010");
        var second = await NewOrderAsync(client, "+380671110011");
        await SqlAsync($"UPDATE dd_order SET invoice_prefix = 'TST-' WHERE order_id IN ({first}, {second})");

        Assert.Equal("TST-1", (await (await client.PostAsync($"/orders/{first}/invoice", null)).Content.ReadFromJsonAsync<InvoiceDto>())!.Invoice);
        Assert.Equal("TST-2", (await (await client.PostAsync($"/orders/{second}/invoice", null)).Content.ReadFromJsonAsync<InvoiceDto>())!.Invoice);
        Assert.Equal("TST-1", (await (await client.PostAsync($"/orders/{first}/invoice", null)).Content.ReadFromJsonAsync<InvoiceDto>())!.Invoice); // зберігся
        Assert.Equal("TST-1", (await client.GetFromJsonAsync<OrderDetails>($"/orders/{first}"))!.Invoice);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/orders/999999/invoice", null)).StatusCode);
    }

    [MySqlFact]
    public async Task PrintData_ListsTheOrdersInTheirOrder_WithOptionsAndTotals()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var first = await NewOrderAsync(client, "+380671110012", quantity: 2);
        var second = await NewOrderAsync(client, "+380671110013");

        var printed = (await client.GetFromJsonAsync<List<PrintOrderDto>>($"/orders/print?ids={second}&ids={first}&ids=999999"))!;
        Assert.Equal([second, first], printed.Select(p => p.Id));
        var line = Assert.Single(printed[1].Lines);
        Assert.Equal(("Сорочка лляна оверсайз", "Розмір: M", 2), (line.Name, line.Options, line.Quantity));
        Assert.Equal("Ольга Перевірка", printed[1].Customer);
        Assert.NotEmpty(printed[1].Totals);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/orders/print")).StatusCode);
    }

    [MySqlFact]
    public async Task RewardPointsAndCommission_AreAddedOnce_AndTakenBack()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var orderId = await NewOrderAsync(client, "+380671110014");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/orders/{orderId}/reward", null)).StatusCode); // без облікового запису
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/orders/{orderId}/commission", null)).StatusCode); // не від партнера

        await SqlAsync($"UPDATE dd_order SET customer_id = 77, affiliate_id = 88, commission = 45 WHERE order_id = {orderId}");
        await SqlAsync($"UPDATE dd_order_product SET reward = 15 WHERE order_id = {orderId}");
        Assert.True((await (await client.PostAsync($"/orders/{orderId}/reward", null)).Content.ReadFromJsonAsync<OrderCreditsDto>())!.RewardAdded);
        var credits = (await (await client.PostAsync($"/orders/{orderId}/reward", null)).Content.ReadFromJsonAsync<OrderCreditsDto>())!; // друге натискання нічого не додає
        Assert.Equal((15, 77), (credits.RewardPoints, credits.CustomerId));
        await WithDbAsync(async db =>
        {
            var rewards = await db.CustomerRewards.Where(r => r.OrderId == orderId).ToListAsync();
            Assert.Equal(15, Assert.Single(rewards).Points);
        });

        Assert.True((await (await client.PostAsync($"/orders/{orderId}/commission", null)).Content.ReadFromJsonAsync<OrderCreditsDto>())!.CommissionAdded);
        await WithDbAsync(async db =>
        {
            var commission = Assert.Single(await db.CustomerTransactions.Where(t => t.OrderId == orderId).ToListAsync());
            Assert.Equal((88, 45m), (commission.CustomerId, commission.Amount));
        });

        Assert.False((await (await client.DeleteAsync($"/orders/{orderId}/reward")).Content.ReadFromJsonAsync<OrderCreditsDto>())!.RewardAdded);
        Assert.False((await (await client.DeleteAsync($"/orders/{orderId}/commission")).Content.ReadFromJsonAsync<OrderCreditsDto>())!.CommissionAdded);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/orders/999999/credits")).StatusCode);
    }

    [MySqlFact]
    public async Task OnlyTheAdmin_Deletes_AndOnlyAnOrderNothingPointsTo()
    {
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        var orderId = await NewOrderAsync(manager, "+380671110015");
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.DeleteAsync($"/orders/{orderId}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/orders/{orderId}")).StatusCode);
        await WithDbAsync(async db => Assert.False(await db.DdOrderProducts.AnyAsync(p => p.OrderId == orderId)));

        var withWaybill = await NewOrderAsync(manager, "+380671110016");
        await WaybillAsync(withWaybill, "20450000005555");
        var refused = await admin.DeleteAsync($"/orders/{withWaybill}");
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("є ТТН", await DetailAsync(refused));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/orders/{withWaybill}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/orders/999999")).StatusCode);
    }

    [MySqlFact]
    public async Task ShippingCost_FollowsTheNovaPoshtaTariff()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        await SqlAsync($"UPDATE dd_product SET weight = 700, length = 30, width = 20, height = 5 WHERE product_id = {ApiFactory.CategoryProductId}");
        var body = new ShippingCostRequest("Київ", NovaPoshta, [new ShippingCostLine(ApiFactory.CategoryProductId, 2)], 1800);

        // Тариф заглушки: 70 за кожен початий кг (1,4 кг → 2), 0,5 % оголошеної вартості.
        Assert.Equal(149m, (await (await client.PostAsJsonAsync("/orders/new/shipping-cost", body)).Content.ReadFromJsonAsync<ShippingCostDto>())!.Cost);
        var door = body with { ShippingCode = "novaposhta_address.novaposhta_address" };
        Assert.Equal(179m, (await (await client.PostAsJsonAsync("/orders/new/shipping-cost", door)).Content.ReadFromJsonAsync<ShippingCostDto>())!.Cost);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/orders/new/shipping-cost", body with { City = "Атлантида" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/orders/new/shipping-cost", body with { ShippingCode = Pickup })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/orders/new/shipping-cost", body with { Lines = [] })).StatusCode);
    }
}
