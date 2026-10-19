using System.Net;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Customers;
using NewErp.Web.Api;
using NewErp.Web.Pages.Customers;

namespace NewErp.Web.Tests;

public class CustomerPagesTests : WebTestContext
{
    [Fact]
    public void Card_ShowsRiskOrdersAndNotes_AndAddsNote()
    {
        Services.AddScoped<CustomersApi>();
        Api.Respond("/customers/5", HttpStatusCode.OK, new CustomerDetails(
            5, "Олена", "Коваль", "olena@example.com", "0935554433", "Роздріб", true, false, new DateTime(2024, 5, 1),
            [new(7, "Олена Коваль", "Київ", "Відділення №1", "", true)],
            [new(2045, new DateTime(2026, 10, 1), 22, "5) Відправлено", 1500m, "UAH", false), new(2050, new DateTime(2026, 10, 5), 19, "Відмінено", 700m, "UAH", true)],
            new CustomerStatsDto(2, 1, 1500m, new DateTime(2026, 10, 1), new DateTime(2026, 10, 5), "Red", "Потенційний фрод-ризик."),
            [new(new DateTime(2026, 10, 6), "Просила дзвонити ввечері")]));
        Api.Respond("/customers/5/notes", HttpStatusCode.NoContent, method: HttpMethod.Post);

        var page = Render<CustomerCard>(p => p.Add(c => c.Id, 5));

        page.WaitForAssertion(() => Assert.Contains("Київ", page.Find("[data-test=details]").TextContent));
        Assert.Contains("mud-alert-text-error", page.Find("[data-test=risk]").ClassName);
        Assert.Equal(["orders/2045", "orders/2050"], page.FindAll("[data-test=orders] a").Select(a => a.GetAttribute("href")));
        Assert.Contains("Просила дзвонити ввечері", page.Markup);

        page.Find("[data-test=add-note] ").Click();

        page.WaitForAssertion(() => Assert.Contains("\"comment\":\"\"", Api.Bodies["/customers/5/notes"]));
    }
}
