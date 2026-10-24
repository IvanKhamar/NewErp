using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Competitors;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

public class CompetitorPagesTests : WebTestContext
{
    public CompetitorPagesTests()
    {
        Services.AddScoped<CompetitorsApi>();
        Services.AddScoped<OrdersApi>();
        var auth = AddAuthorization();
        auth.SetAuthorized("Менеджер");
        auth.SetRoles("Manager");
    }

    [Fact]
    public void Report_ShowsCompetitorColumns_AndDifference()
    {
        Api.Respond("/competitors/report", HttpStatusCode.OK, new CompetitorReportDto(
            [new(1, "Конкурент А"), new(2, "Конкурент Б")],
            [new(101, "Сукня лляна", 1000, [800, null], 800, 25)]));
        Api.Respond("/competitors/links", HttpStatusCode.OK, new List<CompetitorLinkDto>());
        Api.Respond("/competitors", HttpStatusCode.OK, new List<CompetitorDto> { new(1, "Конкурент А", "https://shop-a.ua/", "", true, 1) });

        var page = Render<Competitors>();

        page.WaitForAssertion(() => Assert.Contains("Конкурент Б", page.Find("[data-test=report] thead").TextContent));
        var cells = page.FindAll("[data-test=report] tbody td").Select(td => td.TextContent.Trim()).ToList();
        Assert.Equal("Сукня лляна", cells[0]);
        Assert.Equal("—", cells[3]); // у конкурента Б ціни немає
        Assert.Equal("+25%", cells[^1]);
    }
}
