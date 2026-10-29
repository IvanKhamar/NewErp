using System.Net;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Web.Pages;

namespace NewErp.Web.Tests;

public class LoginTests : WebTestContext
{
    private IRenderedComponent<Login> RenderAndSubmit(string username, string password)
    {
        var page = Render<Login>();
        var inputs = page.FindAll("input");
        inputs[0].Change(username);
        inputs[1].Change(password);
        page.Find("form").Submit();
        return page;
    }

    [Fact]
    public void WrongPassword_ShowsErrorAndStays()
    {
        Api.Respond("/auth/login", HttpStatusCode.Unauthorized);

        var page = RenderAndSubmit("admin", "wrong");

        page.WaitForAssertion(() => Assert.Contains("Невірний логін або пароль", page.Find("[data-test=login-error]").TextContent));
        Assert.EndsWith("/", Services.GetRequiredService<NavigationManager>().Uri); // нікуди не перейшли
    }

    [Fact]
    public void TooManyFailures_ShowsTheServerExplanation()
    {
        Api.Respond("/auth/login", HttpStatusCode.TooManyRequests,
            new { title = "Забагато невдалих спроб входу", detail = "Забагато невдалих спроб входу. Спробуйте знову через 15 хв." });

        var page = RenderAndSubmit("admin", "wrong");

        page.WaitForAssertion(() => Assert.Contains("через 15 хв", page.Find("[data-test=login-error]").TextContent));
    }

    [Fact]
    public void TemporaryPassword_GoesToChangePassword()
    {
        Api.Respond("/auth/login", HttpStatusCode.OK, new CurrentUser(8, "novyi", "Новий Працівник", Roles.Storekeeper, MustChangePassword: true));

        var page = RenderAndSubmit("novyi", "temp-pass-123");

        var navigation = Services.GetRequiredService<NavigationManager>();
        page.WaitForAssertion(() => Assert.EndsWith("change-password", navigation.Uri));
    }

    [Fact]
    public void ReturnUrlInsideApp_IsFollowed()
    {
        Api.Respond("/auth/login", HttpStatusCode.OK, new CurrentUser(1, "admin", "Адмін", Roles.Admin));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("login?returnUrl=stock");

        var page = RenderAndSubmit("admin", "dev");

        page.WaitForAssertion(() => Assert.EndsWith("/stock", navigation.Uri));
    }

    [Fact]
    public void ReturnUrlFromOutside_IsIgnored()
    {
        Api.Respond("/auth/login", HttpStatusCode.OK, new CurrentUser(1, "admin", "Адмін", Roles.Admin));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("login?returnUrl=https://evil.example.com");

        var page = RenderAndSubmit("admin", "dev");

        page.WaitForAssertion(() => Assert.DoesNotContain("evil", navigation.Uri));
    }
}
