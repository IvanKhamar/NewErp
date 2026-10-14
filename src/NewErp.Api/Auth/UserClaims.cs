using System.Globalization;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.Cookies;

using NewErp.Application.Auth;

namespace NewErp.Api.Auth;

/// <summary>
/// Перетворення користувача на claims (пари «тип: значення», які зберігаються в cookie) і назад.
/// </summary>
public static class UserClaims
{
    private const string FullName = "newerp:full_name";

    public static ClaimsPrincipal ToPrincipal(this CurrentUser user) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(FullName, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme));

    public static int GetUserId(this ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Користувач не увійшов у систему."), CultureInfo.InvariantCulture);

    public static CurrentUser ToCurrentUser(this ClaimsPrincipal principal) => new(
        principal.GetUserId(),
        principal.FindFirstValue(ClaimTypes.Name) ?? "",
        principal.FindFirstValue(FullName) ?? "",
        principal.FindFirstValue(ClaimTypes.Role) ?? "");
}
