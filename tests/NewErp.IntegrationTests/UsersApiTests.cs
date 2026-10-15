using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Auth;

namespace NewErp.IntegrationTests;

/// <summary>Окрема тестова база: тест змінює ролі й доступ користувачів із ApiFactory.</summary>
public class UsersApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task NewWorker_LogsInWithTemporaryPassword_ChangesIt_StaysLoggedIn()
    {
        var admin = await factory.LoginAsync(ApiFactory.Admin);

        var created = await admin.PostAsJsonAsync("/users", new CreateUserRequest("novyi", "Новий", "Працівник", "new@example.com", Roles.Manager));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var access = await created.Content.ReadFromJsonAsync<UserAccessResult>();

        var worker = factory.CreateClient();
        (await worker.PostAsJsonAsync("/auth/login", new LoginRequest("novyi", access!.TemporaryPassword!))).EnsureSuccessStatusCode();
        Assert.True((await worker.GetFromJsonAsync<CurrentUser>("/auth/me"))!.MustChangePassword);

        var change = await worker.PostAsJsonAsync("/auth/change-password", new ChangePasswordRequest(access.TemporaryPassword!, "my-own-password"));
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        // Сесію видано заново, тож людина лишається в системі, але вже без позначки тимчасового пароля.
        Assert.False((await worker.GetFromJsonAsync<CurrentUser>("/auth/me"))!.MustChangePassword);
        var list = await admin.GetFromJsonAsync<List<UserDto>>("/users");
        Assert.Contains(list!, u => u.Username == "novyi" && u.HasAccess && u.Role == Roles.Manager);
    }

    [MySqlFact]
    public async Task RoleChangeAndRevoke_EndOpenSessionsImmediately()
    {
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/orders")).StatusCode);

        (await admin.PutAsJsonAsync($"/users/{ApiFactory.ManagerUserId}/access", new SetAccessRequest(Roles.Storekeeper))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await manager.GetAsync("/orders")).StatusCode); // стара cookie з роллю Manager

        var storekeeper = await factory.LoginAsync(ApiFactory.Manager); // той самий пароль, нова роль
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await storekeeper.GetAsync("/stock/items")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/users/{ApiFactory.ManagerUserId}/access")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await storekeeper.GetAsync("/stock/items")).StatusCode);
        var login = await factory.CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.Manager, ApiFactory.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [MySqlFact]
    public async Task ResetPassword_OldPasswordStopsWorking()
    {
        var admin = await factory.LoginAsync(ApiFactory.Admin);

        var reset = await admin.PostAsJsonAsync($"/users/{ApiFactory.StorekeeperUserId}/reset-password", new ResetPasswordRequest("temporary-123"));
        reset.EnsureSuccessStatusCode();

        var oldLogin = await factory.CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.Storekeeper, ApiFactory.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await factory.CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.Storekeeper, "temporary-123"));
        Assert.True((await newLogin.Content.ReadFromJsonAsync<CurrentUser>())!.MustChangePassword);
    }

    [MySqlFact]
    public async Task Guards_AdminCannotRevokeSelf_OthersCannotManageUsers()
    {
        var admin = await factory.LoginAsync(ApiFactory.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/users/{ApiFactory.ActiveUserId}/access")).StatusCode);

        // Власний користувач: інші тести цього класу змінюють паролі й ролі користувачів з ApiFactory.
        (await admin.PostAsJsonAsync("/users",
            new CreateUserRequest("guard-sklad", "Комірник", "Тест", null, Roles.Storekeeper, "guard-password"))).EnsureSuccessStatusCode();
        var storekeeper = factory.CreateClient();
        (await storekeeper.PostAsJsonAsync("/auth/login", new LoginRequest("guard-sklad", "guard-password"))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/users")).StatusCode);
    }
}
