using NewErp.Application.Auth;
using NewErp.Web.Layout;

namespace NewErp.Web.Tests;

public class NavMenuTests : WebTestContext
{
    // Пункти, по яких видно розділ: склад, виробництво, замовлення, бонуси, користувачі.
    private static readonly string[] Markers = ["Розхідники", "Моделі", "Замовлення", "Бонуси", "Користувачі"];

    [Theory]
    [InlineData(Roles.Admin, new[] { "Розхідники", "Моделі", "Замовлення", "Бонуси", "Користувачі" })]
    [InlineData(Roles.Storekeeper, new[] { "Розхідники", "Моделі" })]
    [InlineData(Roles.Manager, new[] { "Замовлення" })]
    public void ShowsOnlySectionsOfTheRole(string role, string[] visible)
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("Тест");
        auth.SetRoles(role);

        var items = Render<NavMenu>().FindAll(".mud-nav-link-text").Select(e => e.TextContent.Trim()).ToList();

        Assert.Equal(visible, Markers.Where(items.Contains));
        Assert.Equal("Головна", items[0]);
    }
}
