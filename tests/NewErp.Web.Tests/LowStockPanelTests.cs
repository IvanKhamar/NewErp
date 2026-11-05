using System.Net;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Stock;
using NewErp.Web.Api;
using NewErp.Web.Pages.Inventory;

namespace NewErp.Web.Tests;

/// <summary>Панель «Мало на складі» на головній.</summary>
public class LowStockPanelTests : WebTestContext
{
    private static StockItemDto Item(int id) => new(id, $"Льон {id}", "м", null, null, null, null, 2.5m, 10m, true, "", 0);

    public LowStockPanelTests() => Services.AddScoped<StockApi>();

    [Fact]
    public void ShowsTheFirstTen_WithLinks_AndALinkToAll()
    {
        Api.Respond("/stock/items", HttpStatusCode.OK, Enumerable.Range(1, 12).Select(Item).ToList());

        var panel = Render<LowStockPanel>();

        panel.WaitForAssertion(() => Assert.Contains("Мало на складі: 12 поз.", panel.Find("[data-test=low-stock]").TextContent));
        Assert.Contains("href=\"stock/items/10\"", panel.Markup);
        Assert.DoesNotContain("href=\"stock/items/11\"", panel.Markup);
        Assert.Contains("2.5 м (мін. 10)", panel.Find("[data-test=low-stock]").TextContent.Replace(',', '.'));
        Assert.Contains("усі 12", panel.Find("[data-test=low-stock-all]").TextContent);
        Assert.Contains(Api.Urls, u => u.Contains("belowMinimumOnly=true", StringComparison.Ordinal));
    }

    [Fact]
    public void NothingLow_ShowsNothing()
    {
        Api.Respond("/stock/items", HttpStatusCode.OK, new List<StockItemDto>());

        var panel = Render<LowStockPanel>();

        panel.WaitForAssertion(() => Assert.Contains(Api.Requests, r => r.Contains("/stock/items", StringComparison.Ordinal)));
        Assert.Empty(panel.FindAll("[data-test=low-stock]"));
    }
}
