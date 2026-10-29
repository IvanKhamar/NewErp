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

    [Fact]
    public void Materials_ShowShortageInRed_AndWhatTheyAreFor()
    {
        Services.AddScoped<ProductionApi>();
        Api.Respond("/production/plan", HttpStatusCode.OK, new SewingPlanDto(1, [], []));
        Api.Respond("/production/plan/materials", HttpStatusCode.OK, new PlanMaterialsDto(
            [
                new(10, "Тканина льон", "м", 1.6m, 1m, 0m, 0.6m, [new(1, "Сукня лляна", "M", "Крій", 1.6m)]),
                new(20, "Нитки білі", "шт", 0.2m, 5m, 0m, 0m, [new(1, "Сукня лляна", "M", "Пошив", 0.2m)]),
            ],
            ["Розхідника 99 зі специфікації моделі «Шарф» немає на складі, його не враховано."]));

        var page = Render<SewingPlan>();

        page.WaitForAssertion(() => Assert.Contains("Тканина льон", page.Find("[data-test=materials]").TextContent));
        var rows = page.FindAll("[data-test=materials] tbody tr");
        Assert.Contains("mud-error-text", rows[0].InnerHtml); // бракує
        Assert.DoesNotContain("mud-error-text", rows[1].InnerHtml);
        Assert.Contains("Сукня лляна M · крій", rows[0].TextContent);
        Assert.Contains("Розхідника 99", page.Markup);
    }
}
