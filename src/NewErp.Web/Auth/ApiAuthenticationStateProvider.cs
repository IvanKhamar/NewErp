using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;

using NewErp.Application.Auth;

namespace NewErp.Web.Auth;

/// <summary>
/// Хто увійшов. Blazor питає це перед кожною сторінкою з [Authorize]. Саму сесію тримає cookie, яку видає API,
/// тож тут лише запам'ятовуємо відповідь GET /auth/me і оновлюємо її після входу, виходу чи 401.
/// </summary>
public sealed class ApiAuthenticationStateProvider(HttpClient http) : AuthenticationStateProvider
{
    private const string MustChangePasswordClaim = "newerp:must_change_password";

    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private Task<AuthenticationState>? _state;

    /// <summary>Поточний користувач, якщо вже відомий (null — не увійшов або ще не перевіряли).</summary>
    public CurrentUser? User { get; private set; }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state ??= LoadAsync();

    /// <returns>Користувач або null, якщо логін чи пароль неправильні.</returns>
    public async Task<CurrentUser?> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("/auth/login", request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return null;
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<CurrentUser>();
        SetUser(user);
        return user;
    }

    public async Task LogoutAsync()
    {
        await http.PostAsync("/auth/logout", null);
        SetUser(null);
    }

    /// <summary>Пароль змінено: прибираємо позначку тимчасового пароля без зайвого запиту до API.</summary>
    public void PasswordChanged()
    {
        if (User is not null)
            SetUser(User with { MustChangePassword = false });
    }

    /// <summary>API відповів 401 (сесія скінчилась або доступ змінили): вважаємо, що користувач вийшов.</summary>
    public void SessionEnded() => SetUser(null);

    private async Task<AuthenticationState> LoadAsync()
    {
        var response = await http.GetAsync("/auth/me");
        if (!response.IsSuccessStatusCode)
            return Anonymous;

        User = await response.Content.ReadFromJsonAsync<CurrentUser>();
        return ToState(User);
    }

    private void SetUser(CurrentUser? user)
    {
        User = user;
        _state = Task.FromResult(ToState(user));
        NotifyAuthenticationStateChanged(_state);
    }

    private static AuthenticationState ToState(CurrentUser? user) =>
        user is null
            ? Anonymous
            : new(new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim(MustChangePasswordClaim, user.MustChangePassword ? "1" : "0"),
                ],
                authenticationType: "NewErp")));
}
