using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Common;
using NewErp.Application.Delivery;
using NewErp.Application.Orders;
using NewErp.Application.Sms;
using NewErp.Domain.Delivery;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

public class OrderPagesTests : WebTestContext
{
    private readonly Bunit.TestDoubles.BunitAuthorizationContext _auth;

    public OrderPagesTests()
    {
        Services.AddScoped<OrdersApi>();
        _auth = AddAuthorization();
        _auth.SetAuthorized("Менеджер");
        _auth.SetRoles("Manager");
        Api.Respond("/orders/statuses", HttpStatusCode.OK, new List<OrderStatusDto> { new(24, "1) Уточнюємо", "#48f9f9"), new(18, "2) Прийнято", "#ffff00") });
        Api.Respond("/orders/sources", HttpStatusCode.OK, new List<LookupItem>());
        Api.Respond("/orders/managers", HttpStatusCode.OK, new List<LookupItem> { new(1, "Марта") });
        Api.Respond("/orders/291/sms", HttpStatusCode.OK, new List<SmsDto>());
        Api.Respond("/orders/291/shipment", HttpStatusCode.NoContent); // ТТН ще немає
        Api.Respond("/orders/291/shipping-check", HttpStatusCode.OK, new ShippingCheckDto(291, 18, "Немира Скиба", "+380678708466", "", []));
        Api.Respond("/orders/291/credits", HttpStatusCode.OK, new OrderCreditsDto(291, 0, 0, false, 0, 0, false));
    }

    [Fact]
    public void ChangeStatus_ShowsStockWarnings()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        Api.Respond("/orders/291/sms", HttpStatusCode.OK, new List<SmsDto>());
        Api.Respond("/orders/291/status", HttpStatusCode.OK,
            new ChangeOrderStatusResult(["Пошитих «Сукня» розміру M бракує 1 шт.: списано 1 з 2."]));

        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForAssertion(() => Assert.Equal("1) Уточнюємо", page.Find("[data-test=status]").TextContent.Trim()));
        Assert.True(page.Find("[data-test=change-status]").HasAttribute("disabled")); // той самий статус — змінювати нічого

        // Випадний список MudBlazor відкривається через JavaScript, тож у тесті обираємо значення напряму.
        var select = page.FindComponent<MudBlazor.MudSelect<int>>();
        page.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(18));
        page.Find("[data-test=change-status]").Click();

        page.WaitForAssertion(() => Assert.Contains("бракує 1 шт.", page.Find("[data-test=warnings]").TextContent));
        Assert.Contains("\"statusId\":18", Api.Bodies["/orders/291/status"]);
    }

    [Fact]
    public void List_SendsPagingToServer()
    {
        Api.Respond("/orders", HttpStatusCode.OK, new PagedResult<OrderListItem>(
            [new OrderListItem(291, DateTime.Now, "Немира Скиба", "+380678708466", 13200, "UAH", 24, "1) Уточнюємо", "Сайт", null, null)],
            1, 20, 400));

        var page = Render<OrderList>();

        page.WaitForAssertion(() => Assert.Contains("Немира Скиба", page.Find("tbody").TextContent));
        Assert.Contains("1–20 з 400", page.Markup);

        // Колір статусу зі старого сайту; на світлому фоні текст чорний, щоб читався.
        var style = page.Find("tbody [data-test=status]").GetAttribute("style");
        Assert.Contains("background-color: #48f9f9", style);
        Assert.Contains("color: #000", style);
    }

    [Fact]
    public void Card_ShowsTtn_AndAsksBeforeSmsWithForeignTtn()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        Api.Respond("/orders/291/shipment", HttpStatusCode.OK, new ShipmentDto(
            1, 291, "novaposhta", "20450000000291", 70, "Відправлення отримано", 9, ShipmentTone.Success, true,
            "Конотоп, №1", false, null, null, null));
        Api.Respond("/orders/291/sms", HttpStatusCode.OK, new SendSmsResultDto(false, null, new TtnMismatchDto("20450000000007", 7)), HttpMethod.Post);

        var provider = Render<MudBlazor.MudDialogProvider>();
        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForAssertion(() => Assert.Equal("20450000000291", page.Find("[data-test=ttn]").TextContent.Trim()));
        Assert.Contains("mud-chip-color-success", page.Find("[data-test=shipment-status]").ClassName);

        // На картці кілька текстових полів (код сертифіката теж), тож беремо саме поле SMS.
        var text = page.FindComponents<MudBlazor.MudTextField<string>>().Single(t => t.Instance.Label == "Текст");
        page.InvokeAsync(() => text.Instance.ValueChanged.InvokeAsync("Ваша ТТН 20450000000007"));
        page.Find("[data-test=send-sms]").Click();

        provider.WaitForAssertion(() => Assert.Contains("належить замовленню №7", provider.Markup));
        Assert.DoesNotContain("\"force\":true", Api.Bodies["/orders/291/sms"]);
        provider.FindAll("button").Single(b => b.TextContent.Contains("Надіслати")).Click();
        page.WaitForAssertion(() => Assert.Contains("\"force\":true", Api.Bodies["/orders/291/sms"]));
    }

    [Fact]
    public async Task Card_EditsComposition_SizeChangeTakesOptionPrice()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 18));
        Api.Respond("/orders/291/composition", HttpStatusCode.OK, new OrderCompositionDto(291, 18,
            [new(101, 501, "Сукня лляна", "SL-01", "S", 2, 1500, [new(50, 501, "Розмір", "S", 1, 0), new(50, 502, "Розмір", "M", 2, 1600)])],
            ShippingCost: 0, Discount: 100, Prepayment: 0, Total: 2900));
        Api.Respond("/orders/291/composition", HttpStatusCode.OK, new UpdateOrderCompositionResult(3100, []), HttpMethod.Put);

        var provider = Render<MudBlazor.MudDialogProvider>();
        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForElement("[data-test=edit-composition]").Click();
        provider.WaitForAssertion(() => Assert.Contains("Разом: 2900", provider.Find("[data-test=total]").TextContent));

        var size = provider.FindComponent<MudBlazor.MudSelect<int?>>();
        await provider.InvokeAsync(() => size.Instance.ValueChanged.InvokeAsync(502));
        provider.WaitForAssertion(() => Assert.Contains("Разом: 3100", provider.Find("[data-test=total]").TextContent));
        provider.Find("[data-test=save]").Click();

        provider.WaitForAssertion(() => Assert.Contains("/orders/291/composition", Api.Bodies.Keys));
        var body = Api.Bodies["/orders/291/composition"];
        Assert.Contains("\"productOptionValueId\":502", body);
        Assert.Contains("\"price\":1600", body);
        Assert.Contains("\"discount\":100", body);
    }

    [Fact]
    public void Card_CreatesShipmentFromPrefilledDialog()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        var city = new CityDto("city-konotop", "Конотоп");
        var branch = new WarehouseDto("wh-1", "Відділення №1", "Branch", 30);
        Api.Respond("/orders/291/shipment/form", HttpStatusCode.OK, new ShipmentFormDto(
            "Немира", "Скиба", "+380678708466", "Конотоп", city, "Відділення №1", branch, 13200, 0,
            [new SenderOption("test", "Тестовий відправник", true)], [new DropoffPointOption("test", "Тестове відділення", true)],
            SeatPreset.All, CanCreate: true, TestMode: true));
        Api.Respond("/orders/291/shipment", HttpStatusCode.OK, new ShipmentDto(
            5, 291, "novaposhta", "00123456789012", 0, "", 0, ShipmentTone.Info, false, "", false, null, null, null), HttpMethod.Post);

        var provider = Render<MudBlazor.MudDialogProvider>();
        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForElement("[data-test=create-shipment]").Click();

        provider.WaitForAssertion(() => Assert.NotNull(provider.Find("[data-test=fake-warning]")));
        provider.Find("[data-test=create]").Click();

        page.WaitForAssertion(() => Assert.Equal("00123456789012", page.Find("[data-test=ttn]").TextContent.Trim()));
        var body = Api.Bodies["/orders/291/shipment"];
        Assert.Contains("\"cityRef\":\"city-konotop\"", body);
        Assert.Contains("\"warehouseRef\":\"wh-1\"", body);
        Assert.Contains("\"seats\":[{\"weight\":1,\"width\":31", body); // типова коробка «маленька»
    }

    [Fact]
    public void ShipmentDialog_SendsTheAddress_ForCourierDelivery()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        var city = new CityDto("city-konotop", "Конотоп");
        Api.Respond("/orders/291/shipment/form", HttpStatusCode.OK, new ShipmentFormDto(
            "Немира", "Скиба", "+380678708466", "Конотоп", city, "", null, 13200, 0,
            [new SenderOption("test", "Тестовий відправник", true)], [new DropoffPointOption("test", "Тестове відділення", true)],
            SeatPreset.All, CanCreate: true, TestMode: true));
        Api.Respond("/orders/291/shipment", HttpStatusCode.OK, new ShipmentDto(
            5, 291, "novaposhta", "00123456789012", 0, "", 0, ShipmentTone.Info, false, "", false, null, null, null), HttpMethod.Post);

        var provider = Render<MudBlazor.MudDialogProvider>();
        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForElement("[data-test=create-shipment]").Click();
        provider.WaitForAssertion(() => Assert.NotNull(provider.Find("[data-test=fake-warning]")));

        var kind = provider.FindComponent<MudBlazor.MudRadioGroup<bool>>();
        provider.InvokeAsync(() => kind.Instance.ValueChanged.InvokeAsync(true));
        provider.WaitForAssertion(() => Assert.NotNull(provider.Find("[data-test=door-address]")));
        var street = provider.FindComponent<MudBlazor.MudAutocomplete<StreetDto>>();
        provider.InvokeAsync(() => street.Instance.ValueChanged.InvokeAsync(new StreetDto("s1", "вул. Хрещатик")));
        var building = provider.FindComponents<MudBlazor.MudTextField<string>>().Single(f => f.Instance.Label == "Будинок");
        provider.InvokeAsync(() => building.Instance.ValueChanged.InvokeAsync("7"));
        provider.Find("[data-test=create]").Click();

        page.WaitForAssertion(() => Assert.Equal("00123456789012", page.Find("[data-test=ttn]").TextContent.Trim()));
        var body = Api.Bodies["/orders/291/shipment"];
        Assert.Contains("\"toDoor\":true", body);
        Assert.Contains("\"streetRef\":\"s1\"", body);
        Assert.Contains("\"building\":\"7\"", body);
    }

    [Fact]
    public void Card_CreatesInternationalWaybill_AndHidesNovaPoshtaPrinting()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        var anna = new InternationalShipment.RecipientInfo("Anna", "Kowalska", "48600100200", "", "PL", "", "Kraków", "Floriańska", "", "", "31-019");
        Api.Respond("/orders/291/shipment/international/form", HttpStatusCode.OK, new InternationalFormDto(anna, new InternationalShipment.TermsInfo(),
            [new InternationalShipment.Item("Сукня лляна", "", "", 0, 1m, 1500m)], 1500m, "UAH", SeatPreset.All, CanCreate: true, Test: true));
        Api.Respond("/delivery/international/countries", HttpStatusCode.OK, new List<CountryDto> { new("PL", "Польща") });
        Api.Respond("/orders/291/shipment/international", HttpStatusCode.OK, new ShipmentDto(
            6, 291, ShipmentDocument.International, "TEST0000000001", 1500, "", 0, ShipmentTone.Info, false, "", false, null, null, null), HttpMethod.Post);

        var provider = Render<MudBlazor.MudDialogProvider>();
        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForElement("[data-test=create-international]").Click();

        provider.WaitForAssertion(() => Assert.NotNull(provider.Find("[data-test=fake-warning]")));
        provider.Find("[data-test=create]").Click();

        page.WaitForAssertion(() => Assert.Equal("TEST0000000001", page.Find("[data-test=ttn]").TextContent.Trim()));
        Assert.Contains("Nova Post International", page.Find("[data-test=carrier]").TextContent);
        Assert.DoesNotContain("Друк ТТН", page.Markup);
        var body = Api.Bodies["/orders/291/shipment/international"];
        Assert.Contains("\"countryCode\":\"PL\"", body);
        Assert.Contains("\"incoterm\":\"DAP\"", body);
        Assert.Contains("\"parcels\":[{\"weight\":1,\"width\":31", body); // типова коробка «маленька»
    }

    [Fact]
    public void Card_IssuesCheque_AndShowsLinkedPayments()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        Api.Respond("/orders/291/cheque", HttpStatusCode.NoContent, method: HttpMethod.Get);
        Api.Respond("/orders/291/cheque", HttpStatusCode.OK, new NewErp.Application.Cashbox.ChequeDto(
            3, 291, "Немира Скиба", 1, "Олена", null, null, 13200, "r-1", true, "https://checkbox.test/r-1", 1, 0, DateTime.Now), HttpMethod.Post);
        Api.Respond("/orders/291/payments", HttpStatusCode.OK, new List<NewErp.Application.Payments.BankPaymentDto>
        {
            new(5, "tx", "UA00", 13200, 0, "Немира Скиба", "Оплата", new DateTime(2026, 10, 15, 10, 0, 0), 291, "Немира Скиба", true),
        });

        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForAssertion(() => Assert.Contains("Немира Скиба", page.Find("[data-test=payment]").TextContent));

        // Після видачі картка перечитує чек: тепер GET теж повертає фіскалізований.
        Api.Respond("/orders/291/cheque", HttpStatusCode.OK, new NewErp.Application.Cashbox.ChequeDto(
            3, 291, "Немира Скиба", 1, "Олена", null, null, 13200, "r-1", true, "https://checkbox.test/r-1", 1, 0, DateTime.Now), HttpMethod.Get);
        page.Find("[data-test=issue-cheque]").Click();

        page.WaitForAssertion(() => Assert.Contains("Чек №3", page.Find("[data-test=cheque]").TextContent));
        Assert.Contains("Відкрити чек", page.Find("[data-test=cheque]").TextContent);
    }

    [Fact]
    public void Card_CreatesLiqPayLink_AndChecksPayment()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        Api.Respond("/orders/291/liqpay-link", HttpStatusCode.OK,
            new NewErp.Application.Payments.PaymentLinkDto("https://www.liqpay.ua/api/3/checkout?data=x&signature=y", 13200, "UAH", true), HttpMethod.Post);
        Api.Respond("/orders/291/liqpay-status", HttpStatusCode.OK,
            new NewErp.Application.Payments.LiqPayStatusDto(NewErp.Domain.Payments.LiqPayState.Paid, "Оплачено", 13200, new DateTime(2026, 10, 20, 9, 30, 0), true));

        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForElement("[data-test=liqpay-create]").Click();

        page.WaitForAssertion(() => Assert.Contains("відкрити сторінку оплати", page.Find("[data-test=liqpay-link]").TextContent));
        Assert.Contains("посилання тестове", page.Find("[data-test=liqpay]").TextContent);
        Assert.Contains("\"amount\":13200", Api.Bodies["/orders/291/liqpay-link"]); // типово вся сума замовлення

        page.Find("[data-test=liqpay-check]").Click();
        page.WaitForAssertion(() => Assert.Contains("Оплачено", page.Find("[data-test=liqpay-status]").TextContent));
    }

    [Fact]
    public void Card_ShowsSmsStubHint_OnlyWithoutSmsService()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        Api.Respond("/sms/settings", HttpStatusCode.OK, new SmsSettingsDto(false));
        var stub = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        stub.WaitForElement("[data-test=sms-stub]");

        Api.Respond("/sms/settings", HttpStatusCode.OK, new SmsSettingsDto(true));
        var real = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        real.WaitForElement("[data-test=send-sms]");
        Assert.Empty(real.FindAll("[data-test=sms-stub]"));
    }

    // ── Інструменти сторінки замовлення (пункти 35–42) ──

    [Fact]
    public void ChangeStatus_AsksToConfirm_AShippingProblem()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        Api.Respond("/orders/291/shipping-check", HttpStatusCode.OK, new ShippingCheckDto(291, 18, "Немира Скиба", "+380678708466", "",
            [new ShippingIssueDto("danger", "ТТН для цього замовлення не створена")]));
        Api.Respond("/orders/291/status", HttpStatusCode.OK, new ChangeOrderStatusResult([]));

        var provider = Render<MudBlazor.MudDialogProvider>();
        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForAssertion(() => Assert.Equal("1) Уточнюємо", page.Find("[data-test=status]").TextContent.Trim()));
        var select = page.FindComponent<MudBlazor.MudSelect<int>>();
        page.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(18));

        page.Find("[data-test=change-status]").Click();
        provider.WaitForAssertion(() => Assert.Contains("ТТН для цього замовлення не створена", provider.Markup));
        provider.FindAll("button").Single(b => b.TextContent.Contains("Скасувати")).Click();
        Assert.DoesNotContain("/orders/291/status", Api.Requests); // скасували — статус не чіпаємо

        page.Find("[data-test=change-status]").Click();
        provider.WaitForAssertion(() => Assert.Contains("ТТН для цього замовлення не створена", provider.Markup));
        provider.FindAll("button").Single(b => b.TextContent.Trim() == "Змінити").Click();
        page.WaitForAssertion(() => Assert.Contains("\"statusId\":18", Api.Bodies["/orders/291/status"]));
    }

    [Fact]
    public void Card_GivesAnInvoiceNumber_AndAddsRewardPoints()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        Api.Respond("/orders/291/invoice", HttpStatusCode.OK, new InvoiceDto("INV-2026-7"), HttpMethod.Post);
        Api.Respond("/orders/291/credits", HttpStatusCode.OK, new OrderCreditsDto(291, 77, 15, false, 0, 0, false));
        Api.Respond("/orders/291/reward", HttpStatusCode.OK, new OrderCreditsDto(291, 77, 15, true, 0, 0, false), HttpMethod.Post);

        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForAssertion(() => page.Find("[data-test=create-invoice]"));
        Assert.Empty(page.FindAll("[data-test=add-commission]")); // замовлення не від партнера
        Assert.Contains("orders/print/invoice?ids=291", page.Find("[data-test=print-invoice]").GetAttribute("href"));

        page.Find("[data-test=create-invoice]").Click();
        page.WaitForAssertion(() => Assert.Contains("/orders/291/invoice", Api.Requests));
        page.Find("[data-test=add-reward]").Click();
        page.WaitForAssertion(() => Assert.Contains("нараховано", page.Find("[data-test=credits]").TextContent));
        Assert.NotEmpty(page.FindAll("[data-test=remove-reward]"));
    }

    [Fact]
    public void Card_EditsTheCustomer_InADialog()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        Api.Respond("/orders/new/methods", HttpStatusCode.OK, new OrderMethodsDto(
            [new("novaposhta_warehouse.novaposhta_warehouse", "Нова Пошта")], [new("cod", "Накладений платіж")]));
        Api.Respond("/orders/291/customer", HttpStatusCode.OK, new OrderCustomerDto(291, "Немира", "Скиба", "+380678708466", "", "",
            "novaposhta_warehouse.novaposhta_warehouse", "Нова Пошта", "Конотоп", "", "Відділення №1", "", "", "cod", "Накладений платіж", ""), HttpMethod.Get);
        Api.Respond("/orders/291/customer", HttpStatusCode.NoContent, method: HttpMethod.Put);

        var provider = Render<MudBlazor.MudDialogProvider>();
        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForAssertion(() => page.Find("[data-test=edit-customer]"));
        page.Find("[data-test=edit-customer]").Click();

        provider.WaitForAssertion(() => provider.Find("[data-test=save-customer]"));
        provider.Find("[data-test=save-customer]").Click();
        page.WaitForAssertion(() => Assert.True(Api.Bodies.ContainsKey("/orders/291/customer")));
        // Кирилицю JSON екранує (Н…), тож тіло читаємо назад у запит.
        var sent = System.Text.Json.JsonSerializer.Deserialize<UpdateOrderCustomerRequest>(Api.Bodies["/orders/291/customer"],
            System.Text.Json.JsonSerializerOptions.Web)!;
        Assert.Equal(("Нова Пошта", "Накладений платіж", "Конотоп"), (sent.ShippingMethod, sent.PaymentMethod, sent.City));
    }

    [Fact]
    public void DeleteButton_IsOnlyForTheAdmin()
    {
        Api.Respond("/orders/291", HttpStatusCode.OK, Order(statusId: 24));
        var page = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        page.WaitForAssertion(() => page.Find("[data-test=edit-customer]"));
        Assert.Empty(page.FindAll("[data-test=delete-order]"));

        _auth.SetRoles("Admin");
        Api.Respond("/orders/291", HttpStatusCode.OK, new ChangeOrderStatusResult([]), HttpMethod.Delete);
        var provider = Render<MudBlazor.MudDialogProvider>();
        var admin = Render<OrderCard>(p => p.Add(c => c.Id, 291));
        admin.WaitForAssertion(() => admin.Find("[data-test=delete-order]"));
        admin.Find("[data-test=delete-order]").Click();
        provider.WaitForAssertion(() => Assert.Contains("не можна скасувати", provider.Markup));
        provider.FindAll("button").Single(b => b.TextContent.Trim() == "Видалити").Click();
        admin.WaitForAssertion(() => Assert.EndsWith("/orders", Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri));
    }

    [Fact]
    public void PrintPage_ShowsOnePagePerOrder_AndTheInvoiceTotals()
    {
        var store = new PrintStoreDto("Vidro", "Київ", "+380000000000", "shop@example.test");
        Api.Respond("/orders/print", HttpStatusCode.OK, new List<PrintOrderDto>
        {
            new(291, "INV-1", DateTime.Now, "Немира Скиба", "+380678708466", "", "Нова Пошта", "Конотоп, Відділення №1", "Накладений платіж", "",
                "UAH", store, [new PrintLineDto("Сукня лляна", "VD-1", "Розмір: M", 2, 4400, 8800)], [new PrintTotalDto("Разом", 8800)]),
            new(292, null, DateTime.Now, "Ольга Перевірка", "", "", "Самовивіз", "", "Готівка", "", "UAH", store, [], []),
        });
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo("orders/print/invoice?ids=291&ids=292");

        var page = Render<OrderPrint>(p => p.Add(c => c.Kind, "invoice"));

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[data-test=page]").Count));
        Assert.Contains("Розмір: M", page.Markup);
        Assert.Contains("Рахунок № INV-1", page.Markup);
        Assert.Contains("/orders/print?ids=291&ids=292", Api.Urls);

        var other = Render<OrderPrint>(p => p.Add(c => c.Kind, "other"));
        Assert.NotEmpty(other.FindAll("[data-test=unknown-kind]"));
    }

    [Fact]
    public void Discrepancies_ShowTheThreeGroups()
    {
        Api.Respond("/orders/discrepancies", HttpStatusCode.OK, new List<DiscrepancyGroupDto>
        {
            new("Стара ТТН", [new DiscrepancyDto(291, "Немира Скиба", "warning", "ТТН створена 01.11 10:00")]),
            new("Відправлено", []),
            new("Чекає на відправку", []),
        });

        var page = Render<Discrepancies>();

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("[data-test=group]").Count));
        Assert.Contains("ТТН створена 01.11 10:00", page.Find("[data-test=item]").TextContent);
        Assert.Contains("/orders/discrepancies?days=3", Api.Urls);
    }

    private static OrderDetails Order(int statusId) => new(
        291, DateTime.Now, "Немира Скиба", "+380678708466", "", "Нова Пошта", "Конотоп", "", "Monobank", "",
        13200, "UAH", statusId, statusId == 24 ? "1) Уточнюємо" : "2) Прийнято", 1, "Сайт", null, null,
        [new OrderProductLine(101, "Сукня лляна", "VD-1", 2, 4400, 8800)], []);
}
