using System.Net;

using Microsoft.Extensions.DependencyInjection;

using MudBlazor;

using NewErp.Application.Catalog;
using NewErp.Domain.Catalog;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

public class PreOrderPageTests : WebTestContext
{
    public PreOrderPageTests()
    {
        Services.AddScoped<PreOrdersApi>();
        Api.Respond("/preorders", HttpStatusCode.OK, new List<PreOrderRow>
        {
            new(502, 102, "Сорочка лляна", "T-2", "M", -2, 5, new DateOnly(2026, 11, 20), SizeAvailability.PreOrder),
        });
        Api.Respond("/preorders/502", HttpStatusCode.NoContent, method: HttpMethod.Put);
    }

    [Fact]
    public async Task ShowsSoldAhead_AndSavesChangedQuota()
    {
        var page = Render<PreOrders>();
        page.WaitForAssertion(() => Assert.Contains("передзамовлення", page.Find("[data-test=sizes]").TextContent));
        Assert.Contains("продано наперед: 2", page.Find("[data-test=sizes]").TextContent);

        var quota = page.FindComponent<MudNumericField<int>>();
        await page.InvokeAsync(() => quota.Instance.ValueChanged.InvokeAsync(8));
        await page.InvokeAsync(() => page.FindAll("[data-test=sizes] button").Last().Click());

        page.WaitForAssertion(() => Assert.Contains("/preorders/502", Api.Bodies.Keys));
        Assert.Contains("\"quota\":8", Api.Bodies["/preorders/502"]);
        Assert.Contains("\"dateAvailable\":\"2026-11-20\"", Api.Bodies["/preorders/502"]);
    }
}
