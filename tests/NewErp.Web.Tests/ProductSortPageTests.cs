using System.Net;

using Microsoft.Extensions.DependencyInjection;

using MudBlazor;

using NewErp.Application.Catalog;
using NewErp.Domain.Catalog;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

public class ProductSortPageTests : WebTestContext
{
    private static readonly SortGroupDto Dresses = new(SortGroupType.Category, "30", "Сукні");

    public ProductSortPageTests()
    {
        Services.AddScoped<ProductSortApi>();
        Api.Respond("/catalog/sort/groups", HttpStatusCode.OK, new List<SortGroupDto> { Dresses });
        Api.Respond("/catalog/sort/crosssell-metric", HttpStatusCode.OK, new CrossSellMetricDto(false));
        Api.Respond("/catalog/sort/pins", HttpStatusCode.OK, new List<SortPinDto> { new(7, SortGroupType.Category, "30", 1, 203, "Сукня 203") });
        Api.Respond("/catalog/sort/snapshots", HttpStatusCode.OK, new List<SortSnapshotDto> { new(4, "Порядок групи: Сукні", new DateTime(2026, 10, 18, 9, 5, 0)) });
        Api.Respond("/catalog/sort/preview", HttpStatusCode.OK, new List<SortPreviewRow>
        {
            new(203, "Сукня 203", "", 0, true, 3, 1),
            new(202, "Сукня 202", "", 3, false, 1, 2),
        });
        Api.Respond("/catalog/sort/apply", HttpStatusCode.OK, new SortResult(5, 2), HttpMethod.Post);
    }

    [Fact]
    public async Task Preview_CanBeReorderedByHand_ThenApplied()
    {
        var page = Render<ProductSort>();
        page.WaitForAssertion(() => Assert.Contains("Сукня 203", page.Find("[data-test=pins]").TextContent));
        Assert.Contains("Сукні", page.Find("[data-test=pins]").TextContent);
        Assert.Contains("18.10.2026 09:05", page.Find("[data-test=snapshots]").TextContent);

        var select = page.FindComponent<MudSelect<SortGroupDto>>();
        await page.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(Dresses));
        page.WaitForAssertion(() => Assert.Contains("закріплено", page.Find("[data-test=preview-table]").TextContent));

        // Друга кнопка першого рядка — «вниз».
        await page.InvokeAsync(() => page.Find("[data-test=preview-table] tbody tr button:nth-of-type(2)").Click());
        await page.InvokeAsync(() => page.Find("button[data-test=apply]").Click());

        page.WaitForAssertion(() => Assert.Contains("/catalog/sort/apply", Api.Bodies.Keys));
        Assert.Contains("\"productIds\":[202,203]", Api.Bodies["/catalog/sort/apply"]);
        Assert.Contains("\"key\":\"30\"", Api.Bodies["/catalog/sort/apply"]);
    }
}
