using System.Net;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Orders;
using NewErp.Application.Reports;
using NewErp.Web.Api;
using NewErp.Web.Pages.Reports;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

/// <summary>Вкладки UTM і вітрини, стандартні звіти й блок ремаркетингу картки замовлення.</summary>
public class StoreReportPagesTests : WebTestContext
{
    public StoreReportPagesTests()
    {
        Services.AddScoped<ReportsApi>();
        Services.AddScoped<OrdersApi>();
    }

    private static UtmReportDto Utm() => new(
        new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
        [new UtmReportRow(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), "google", 2, 3, 3500)], 2, 3500, ["google", "ig"], ["cpc"], ["brand"]);

    [Fact]
    public void Utm_ShowsRows_AndAsksAgainBySource()
    {
        Api.Respond("/reports/utm", HttpStatusCode.OK, Utm());

        var tab = Render<UtmReport>(p => p.Add(c => c.Filter, new ReportFilter()));

        tab.WaitForAssertion(() => Assert.Contains(Fmt.Money(3500), tab.Find("[data-test=utm]").TextContent));
        Assert.DoesNotContain("<th>Джерело</th>", tab.Markup);

        var bySource = tab.FindComponent<MudBlazor.MudSwitch<bool>>();
        tab.InvokeAsync(() => bySource.Instance.ValueChanged.InvokeAsync(true));

        tab.WaitForAssertion(() =>
        {
            Assert.Contains("<th>Джерело</th>", tab.Markup);
            Assert.Contains(Api.Urls, u => u.Contains("bySource=true", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Utm_AskFailed_SaysSo()
    {
        var tab = Render<UtmReport>(p => p.Add(c => c.Filter, new ReportFilter()));

        tab.WaitForAssertion(() => Assert.NotNull(tab.Find("[data-test=utm-error]")));
    }

    [Fact]
    public void Online_ShowsAGuest_AndEscapesWhatAVisitorTyped()
    {
        Api.Respond("/reports/online", HttpStatusCode.OK, new List<OnlineVisitorDto>
        {
            new("192.0.2.11", 0, null, "https://shop.test/<script>", "", new DateTime(2026, 11, 2, 10, 5, 0)),
        });

        var tab = Render<StoreReportTab>(p => p.Add(c => c.Kind, StoreReportTab.OnlineKind));

        tab.WaitForAssertion(() => Assert.Contains("гість", tab.Find("[data-test=online]").TextContent));
        Assert.Empty(tab.FindAll("[data-test=online] script"));
        Assert.Contains("https://shop.test/<script>", tab.Find("[data-test=online]").TextContent);
    }

    [Fact]
    public void Views_Rewards_AndTaxes_Show()
    {
        Api.Respond("/reports/product-views", HttpStatusCode.OK, new List<ProductViewsDto> { new(102, "Сорочка", "T-2", 40, 1, 2.5m) });
        Api.Respond("/reports/rewards", HttpStatusCode.OK, new List<CustomerRewardDto> { new(1, "Олена Коваль", 200, new DateTime(2026, 10, 5)) });
        Api.Respond("/reports/taxes", HttpStatusCode.OK, new TaxReportDto(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), [new TaxReportRow(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), "ПДВ 20%", 2, 583.33m)], 583.33m));

        var views = Render<StoreReportTab>(p => p.Add(c => c.Kind, StoreReportTab.ViewsKind));
        views.WaitForAssertion(() => Assert.Contains("2,5", views.Find("[data-test=views]").TextContent));
        var rewards = Render<StoreReportTab>(p => p.Add(c => c.Kind, StoreReportTab.RewardsKind));
        rewards.WaitForAssertion(() => Assert.Contains("Олена Коваль", rewards.Find("[data-test=rewards]").TextContent));
        var taxes = Render<StoreReportTab>(p => p.Add(c => c.Kind, StoreReportTab.TaxesKind));
        taxes.WaitForAssertion(() => Assert.Contains("ПДВ 20%", taxes.Find("[data-test=taxes]").TextContent));
    }

    [Fact]
    public void StandardReports_ShowAnyShape_AndSayWhenCut()
    {
        Api.Respond("/reports/standard/sales-orders", HttpStatusCode.OK, new StandardReportDto(
            "sales-orders", "Продажі за період", ["Період", "Замовлень"], [["2026-10-01", 2]], new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), true));

        var page = Render<StandardReports>();

        page.WaitForAssertion(() => Assert.Contains("2026-10-01", page.Find("[data-test=report]").TextContent));
        Assert.Contains("Замовлень", page.Find("[data-test=report]").TextContent);
        Assert.NotNull(page.Find("[data-test=cut]"));
    }

    [Fact]
    public void RemarketingPanel_ShowsTags_AndEvents()
    {
        Api.Respond("/orders/291/remarketing", HttpStatusCode.OK, new OrderRemarketingDto(
            291, new Dictionary<string, string> { ["utm_source"] = "ig" }, [new PixelEventDto("Google Analytics 4", new DateTime(2026, 10, 1, 12, 5, 0))]));

        var panel = Render<RemarketingPanel>(p => p.Add(c => c.OrderId, 291));

        panel.WaitForAssertion(() => Assert.Contains("Google Analytics 4", panel.Find("[data-test=remarketing]").TextContent));
        Assert.Contains("utm_source", panel.Markup);
    }

    [Fact]
    public void RemarketingPanel_WithoutData_SaysSo()
    {
        var panel = Render<RemarketingPanel>(p => p.Add(c => c.OrderId, 291));

        panel.WaitForAssertion(() => Assert.Contains("Даних немає", panel.Find("[data-test=remarketing]").TextContent));
    }
}
