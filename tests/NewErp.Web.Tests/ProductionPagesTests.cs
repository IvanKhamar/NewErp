using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Production;
using NewErp.Domain.Production;
using NewErp.Web.Api;
using NewErp.Web.Pages.Manufacturing;

namespace NewErp.Web.Tests;

public class ProductionPagesTests : WebTestContext
{
    public ProductionPagesTests()
    {
        Services.AddScoped<ProductionApi>();
        var auth = AddAuthorization();
        auth.SetAuthorized("Комірник");
        auth.SetRoles("Storekeeper");
    }

    private static ProductionModelDto Model(string type) => new(
        3, "Костюм", type, 30, 0,
        [new ProductionSizeDto(10, "S", 50, 1, 3, null), new ProductionSizeDto(11, "M", 50, 1, 3, null)],
        [], [], [], []);

    [Fact]
    public void ReadyMade_OffersOnlyIncomeAndShipment()
    {
        var panel = Render<OperationPanel>(p => p.Add(c => c.Model, Model(ProductionModel.ReadyMade)));

        Assert.Equal(["Прихід", "Відвантажено"], panel.FindAll(".mud-chip").Select(c => c.TextContent.Trim()));
    }

    [Fact]
    public void Sewn_PostsQuantitiesOfFilledSizes_AndShowsShortageWarnings()
    {
        Api.Respond("/production/models/3/sewn", HttpStatusCode.OK, new ProductionOperationResult(
            [new SizeChangeDto(11, "M", 3, 8)], ["Бракує розкроєних деталей «Перед» розміру M: 4 шт."]));
        var done = 0;
        var panel = Render<OperationPanel>(p => p
            .Add(c => c.Model, Model(ProductionModel.Clothing))
            .Add(c => c.OnDone, () => done++));

        panel.FindAll(".mud-chip").First(c => c.TextContent.Contains("Пошито")).Click();
        panel.FindAll("tbody input")[1].Change("5"); // розмір M
        panel.Find("[data-test=submit]").Click();

        panel.WaitForAssertion(() => Assert.Contains("«Перед»", panel.Find("[data-test=warnings]").TextContent));
        Assert.Contains("\"sizeId\":11", Api.Bodies["/production/models/3/sewn"]);
        Assert.DoesNotContain("\"sizeId\":10", Api.Bodies["/production/models/3/sewn"]); // порожній розмір не надсилається
        Assert.Equal(1, done);
    }

    [Fact]
    public void Ship_WithWarehouses_SendsChosenWarehouse()
    {
        Api.Respond("/production/models/3/ship", HttpStatusCode.OK, new ProductionOperationResult([new SizeChangeDto(10, "S", 3, 2)], []));
        var panel = Render<OperationPanel>(p => p
            .Add(c => c.Model, Model(ProductionModel.Clothing))
            .Add(c => c.Warehouses, [new Application.Common.LookupItem(7, "Шоурум")]));

        panel.FindAll(".mud-chip").First(c => c.TextContent.Contains("Відвантажено")).Click();
        Assert.Contains("Звідки", panel.Markup);
        panel.FindAll("tbody input")[0].Change("1");
        panel.Find("[data-test=submit]").Click();

        // Без вибору — основний склад, тобто warehouseId: null.
        panel.WaitForAssertion(() => Assert.Contains("\"warehouseId\":null", Api.Bodies["/production/models/3/ship"]));
    }

    [Fact]
    public void SewnByWarehouse_ShowsPlaces()
    {
        Api.Respond("/production/by-warehouse", HttpStatusCode.OK, new SewnByWarehouseDto(
            [new(null, "Основний склад"), new(7, "Шоурум")],
            [new(3, "Костюм", 11, "M", 8, [3, 5])]));

        var page = Render<SewnByWarehouse>();

        page.WaitForAssertion(() => Assert.Contains("Шоурум", page.Find("thead").TextContent));
        Assert.Equal(["Костюм", "M", "8", "3", "5"], page.FindAll("tbody td").Select(td => td.TextContent.Trim()));
    }

    [Fact]
    public void Models_NeedToSewFilter_ShowsOnlyDeficit()
    {
        Api.Respond("/production/models", HttpStatusCode.OK, new List<ProductionModelSummaryDto>
        {
            new(1, "Сукня", ProductionModel.Clothing, 30, 4, 12, 18),
            new(2, "Шопер", ProductionModel.ReadyMade, 10, 0, 15, 0),
        });

        var page = Render<Models>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));

        page.Find("input[type=checkbox]").Change(true);

        Assert.Contains("Сукня", Assert.Single(page.FindAll("tbody tr")).TextContent);
    }

    [Fact]
    public void Models_StorefrontSync_ReportsResult()
    {
        Api.Respond("/production/models", HttpStatusCode.OK, new List<ProductionModelSummaryDto>());
        Api.Respond("/production/storefront/sync", HttpStatusCode.OK, new StorefrontSyncResult(3, 12));
        var snackbar = Render<MudBlazor.MudSnackbarProvider>();
        var page = Render<Models>();

        page.Find("[data-test=storefront-sync]").Click();

        snackbar.WaitForAssertion(() => Assert.Contains("моделей 3, товарів на сайті 12", snackbar.Markup));
    }
}
