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
    public OrderPagesTests()
    {
        Services.AddScoped<OrdersApi>();
        var auth = AddAuthorization();
        auth.SetAuthorized("Менеджер");
        auth.SetRoles("Manager");
        Api.Respond("/orders/statuses", HttpStatusCode.OK, new List<OrderStatusDto> { new(24, "1) Уточнюємо", "#48f9f9"), new(18, "2) Прийнято", "#ffff00") });
        Api.Respond("/orders/sources", HttpStatusCode.OK, new List<LookupItem>());
        Api.Respond("/orders/managers", HttpStatusCode.OK, new List<LookupItem> { new(1, "Марта") });
        Api.Respond("/orders/291/sms", HttpStatusCode.OK, new List<SmsDto>());
        Api.Respond("/orders/291/shipment", HttpStatusCode.NoContent); // ТТН ще немає
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

        var text = page.FindComponent<MudBlazor.MudTextField<string>>();
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

    private static OrderDetails Order(int statusId) => new(
        291, DateTime.Now, "Немира Скиба", "+380678708466", "", "Нова Пошта", "Конотоп", "", "Monobank", "",
        13200, "UAH", statusId, statusId == 24 ? "1) Уточнюємо" : "2) Прийнято", 1, "Сайт", null, null,
        [new OrderProductLine(101, "Сукня лляна", "VD-1", 2, 4400, 8800)], []);
}
