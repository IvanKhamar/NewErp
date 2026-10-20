using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Locations;
using NewErp.Web.Api;
using NewErp.Web.Pages.Locations;

namespace NewErp.Web.Tests;

public class LocationPagesTests : WebTestContext
{
    [Fact]
    public void ClickRow_FillsForm_SaveSendsUpdate()
    {
        Services.AddScoped<LocationsApi>();
        Api.Respond("/locations", HttpStatusCode.OK, new List<LocationDto>
        {
            new(3, "Склад на Подолі", "", "Оксана", "0671234567", "Київ", "вул. Сагайдачного, 1", "склад"),
        });
        Api.Respond("/locations/3", HttpStatusCode.NoContent, method: HttpMethod.Put);

        var page = Render<Locations>();

        page.WaitForAssertion(() => Assert.Contains("Склад на Подолі", page.Find("[data-test=locations]").TextContent));
        Assert.Equal("/locations/export.xlsx", page.Find("[data-test=export]").GetAttribute("href"));
        page.Find("[data-test=locations] tbody tr").Click();
        Assert.Contains("Запис №3", page.Markup);

        page.Find("[data-test=save]").Click();

        page.WaitForAssertion(() => Assert.True(Api.Bodies.ContainsKey("/locations/3")));
        var body = System.Text.Json.JsonSerializer.Deserialize<SaveLocationRequest>(Api.Bodies["/locations/3"], System.Text.Json.JsonSerializerOptions.Web);
        Assert.Equal(("Склад на Подолі", "Київ"), (body!.Name, body.City));
    }
}
