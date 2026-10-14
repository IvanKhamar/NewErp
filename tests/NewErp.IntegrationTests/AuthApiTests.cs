using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Auth;

namespace NewErp.IntegrationTests;

public class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task Login_ThenMe_ReturnsCurrentUser()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var me = await client.GetFromJsonAsync<CurrentUser>("/auth/me");

        Assert.Equal(new CurrentUser(ApiFactory.ManagerUserId, ApiFactory.Manager, "Марта Тест", Roles.Manager), me);
    }

    [MySqlTheory]
    [InlineData(ApiFactory.Admin, "wrong-password")]
    [InlineData(ApiFactory.Inactive, ApiFactory.Password)] // вимкнений користувач не може увійти
    [InlineData("nobody", ApiFactory.Password)]
    public async Task Login_Rejected_Returns401(string username, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest(username, password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [MySqlFact]
    public async Task Anonymous_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [MySqlFact]
    public async Task Logout_EndsSession()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        (await client.PostAsync("/auth/logout", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
    }

    [MySqlTheory]
    [InlineData(ApiFactory.Storekeeper, "/orders", HttpStatusCode.Forbidden)]
    [InlineData(ApiFactory.Storekeeper, "/stock/items", HttpStatusCode.OK)]
    [InlineData(ApiFactory.Manager, "/orders", HttpStatusCode.OK)]
    [InlineData(ApiFactory.Manager, "/stock/items", HttpStatusCode.Forbidden)]
    [InlineData(ApiFactory.Manager, "/staff/bonuses?from=2026-10-01&to=2026-10-31", HttpStatusCode.Forbidden)]
    [InlineData(ApiFactory.Admin, "/staff/bonuses?from=2026-10-01&to=2026-10-31", HttpStatusCode.OK)]
    public async Task Roles_LimitAccessToModules(string username, string url, HttpStatusCode expected)
    {
        var client = await factory.LoginAsync(username);

        Assert.Equal(expected, (await client.GetAsync(url)).StatusCode);
    }
}
