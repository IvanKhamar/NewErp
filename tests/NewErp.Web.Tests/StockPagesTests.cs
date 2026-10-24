using System.Net;

using AngleSharp.Dom;

using MudBlazor;

using NewErp.Application.Common;
using NewErp.Application.Stock;
using NewErp.Web.Api;
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

    [Fact]
    public void ByWarehouse_ShowsColumnPerPlace_AndValues()
    {
        Api.Respond("/stock/by-warehouse", HttpStatusCode.OK, new StockByWarehouseDto(
            [new(null, "Основний склад", 2000), new(7, "Склад на Подолі", 600)],
            [new(1, "Льон", "м", "Тканини", 26, [20, 6])]));

        var page = Render<StockByWarehouse>();

        page.WaitForAssertion(() => Assert.Contains("Склад на Подолі", page.Find("thead").TextContent));
        Assert.Equal(["Льон", "Тканини", "26 м", "20", "6"], page.FindAll("tbody td").Select(td => td.TextContent.Trim()));
        Assert.Contains("600", page.Find("[data-test=values]").TextContent);
    }

    [Fact]
    public void ItemCard_ShowsPlaces()
    {
        Api.Respond("/stock/items/1", HttpStatusCode.OK, Item(1, "Льон", 26, 0));
        Api.Respond("/stock/items/1/movements", HttpStatusCode.OK, new List<StockMovementDto>());
        Api.Respond("/stock/items/1/places", HttpStatusCode.OK,
            new ItemPlacementDto(1, "Льон", "м", 26, [new(null, "Основний склад", 20), new(7, "Склад на Подолі", 6)]));
        Api.Respond("/stock/items/1/transfers", HttpStatusCode.OK, new List<StockTransferDto>());
        Api.Respond("/stock/warehouses", HttpStatusCode.OK, new List<LookupItem> { new(7, "Склад на Подолі") });

        var page = Render<ItemCard>(p => p.Add(c => c.Id, 1));

        page.WaitForAssertion(() => Assert.Contains("Склад на Подолі", page.Find("[data-test=places]").TextContent));
        Assert.Contains("Перемістити", page.Markup);
    }

    [Fact]
    public void Stocktaking_SheetLinkFollowsChosenCategory()
    {
        Api.Respond("/stock/warehouses", HttpStatusCode.OK, new List<LookupItem>());

        var page = Render<Stocktaking>();

        page.WaitForAssertion(() => Assert.Equal("/stock/stocktaking/sheet.xlsx", page.Find("[data-test=sheet]").GetAttribute("href")));
        Assert.Equal("/stock/stocktaking/sheet.xlsx?categoryId=1&warehouseId=7", StockApi.StocktakingSheetUrl(1, 7));
        Assert.DoesNotContain("Записати пораховане", page.Markup); // без завантаженого аркуша записувати нічого
    }

    private static List<IElement> Rows(IRenderedComponent<Items> page) => page.FindAll("tbody tr").ToList();
}
