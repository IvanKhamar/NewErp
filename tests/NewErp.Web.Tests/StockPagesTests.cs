using System.Net;

using AngleSharp.Dom;

using MudBlazor;

using NewErp.Application.Common;
using NewErp.Application.Stock;
using NewErp.Web.Pages.Inventory;

namespace NewErp.Web.Tests;

public class StockPagesTests : WebTestContext
{
    private static StockItemDto Item(int id, string name, decimal quantity, decimal min) =>
        new(id, name, "м", 1, "Тканини", null, null, quantity, min, quantity < min, "", 100);

    public StockPagesTests()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("Комірник");
        auth.SetRoles("Storekeeper");
        Api.Respond("/stock/categories", HttpStatusCode.OK, new List<LookupItem> { new(1, "Тканини") });
    }

    [Fact]
    public void Items_ShowsShortageMarks_AndBelowMinimumFilterHidesTheRest()
    {
        Api.Respond("/stock/items", HttpStatusCode.OK, new List<StockItemDto>
        {
            Item(1, "Льон", 40, 30),
            Item(2, "Трикотаж", 0, 40),
            Item(3, "Віскоза", 5, 20),
        });

        var page = Render<Items>();
        page.WaitForAssertion(() => Assert.Equal(3, Rows(page).Count));
        Assert.Contains("немає", Rows(page)[1].TextContent);
        Assert.Contains("мало", Rows(page)[2].TextContent);

        page.Find("input[type=checkbox]").Change(true); // перемикач «Лише нижче мінімуму» — єдиний на сторінці

        Assert.Equal(["Трикотаж", "Віскоза"], Rows(page).Select(r => r.QuerySelector("td")!.TextContent));
    }

    [Fact]
    public void PurchaseOrder_Draft_CanEditQuantities_Ordered_CanReceive()
    {
        RespondOrder(Domain.Stock.PurchaseOrder.Draft);
        var draft = Render<PurchaseOrderCard>(p => p.Add(c => c.Id, 7));

        draft.WaitForAssertion(() => Assert.Equal("Чернетка", draft.Find("[data-test=status]").TextContent.Trim()));
        Assert.Contains("Надіслано постачальнику", draft.Markup);
        Assert.DoesNotContain("Прийняти на склад", draft.Markup);

        RespondOrder(Domain.Stock.PurchaseOrder.Ordered);
        var ordered = Render<PurchaseOrderCard>(p => p.Add(c => c.Id, 7));

        ordered.WaitForAssertion(() => Assert.Contains("Прийняти на склад", ordered.Markup));
        ordered.FindAll("button").First(b => b.TextContent.Contains("Прийняти на склад")).Click();

        // У полі «приїхало зараз» за замовчуванням те, що ще чекаємо (20 з 60).
        ordered.WaitForAssertion(() => Assert.Contains("\"quantity\":20", Api.Bodies["/stock/purchase-orders/7/receive"]));
    }

    private void RespondOrder(string status)
    {
        Api.Respond("/stock/purchase-orders/7", HttpStatusCode.OK, new PurchaseOrderDto(
            7, 1, "Текстиль-Опт", "", status, "", DateTime.Now, null, null,
            [new PurchaseOrderLineDto(11, 2, "Трикотаж", "м", 60, 180, 10800, Received: 40, Remaining: 20)],
            10800, "Добрий день!"));
        Api.Respond("/stock/purchase-orders/7/receive", HttpStatusCode.NoContent);
    }

    private static List<IElement> Rows(IRenderedComponent<Items> page) => page.FindAll("tbody tr").ToList();
}
