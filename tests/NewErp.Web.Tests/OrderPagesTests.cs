using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Common;
using NewErp.Application.Orders;
using NewErp.Application.Sms;
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

    private static OrderDetails Order(int statusId) => new(
        291, DateTime.Now, "Немира Скиба", "+380678708466", "", "Нова Пошта", "Конотоп", "", "Monobank", "",
        13200, "UAH", statusId, statusId == 24 ? "1) Уточнюємо" : "2) Прийнято", 1, "Сайт", null, null,
        [new OrderProductLine(101, "Сукня лляна", "VD-1", 2, 4400, 8800)], []);
}
