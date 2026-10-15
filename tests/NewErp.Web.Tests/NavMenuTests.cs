using NewErp.Application.Auth;
using NewErp.Web.Layout;

namespace NewErp.Web.Tests;

public class NavMenuTests : WebTestContext
{
    [Theory]
    [InlineData(Roles.Admin, new[] { "Головна", "Склад", "Розхідники", "Що дозамовити", "Замовлення постачальникам", "Постачальники й категорії", "Виробництво", "Замовлення", "Бонуси", "Користувачі" })]
    [InlineData(Roles.Storekeeper, new[] { "Головна", "Склад", "Розхідники", "Що дозамовити", "Замовлення постачальникам", "Постачальники й категорії", "Виробництво" })]
    [InlineData(Roles.Manager, new[] { "Головна", "Замовлення" })]
    public void ShowsOnlySectionsOfTheRole(string role, string[] expected)
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("Тест");
        auth.SetRoles(role);

        var menu = Render<NavMenu>();

        Assert.Equal(expected, menu.FindAll(".mud-nav-link-text").Select(e => e.TextContent.Trim()));
    }
}
