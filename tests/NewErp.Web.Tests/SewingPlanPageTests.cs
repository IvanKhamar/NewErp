using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Production;
using NewErp.Web.Api;
using NewErp.Web.Pages.Manufacturing;

namespace NewErp.Web.Tests;

public class SewingPlanPageTests : WebTestContext
{
    [Fact]
    public void ShowsWhatToSew_OverdueDeadline_OrderLinks_AndIssues()
    {
        Services.AddScoped<ProductionApi>();
        Api.Respond("/production/plan", HttpStatusCode.OK, new SewingPlanDto(3,
            [new(1, "Сукня лляна", 7, "M", 3, 3, 1, 1, 2, 1, 1, new DateOnly(2026, 10, 11), true, [5, 9])],
            [new("Шарф", "Не прив'язано до моделі виробництва.", [4, 6])]));

        var page = Render<SewingPlan>();

        page.WaitForAssertion(() => Assert.Contains("Сукня лляна", page.Find("[data-test=plan]").TextContent));
        var plan = page.Find("[data-test=plan]");
        Assert.Contains("11.10.2026", plan.TextContent);
        Assert.Contains("mud-error-text", plan.InnerHtml);
        Assert.Equal(["orders/5", "orders/9"], plan.QuerySelectorAll("a[href^='orders/']").Select(a => a.GetAttribute("href")));
        Assert.Contains("Не враховано товарів: 1", page.Markup);
    }
}
