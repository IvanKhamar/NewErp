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
    private const string SecurityStamp = "newerp:stamp";
    private const string MustChangePassword = "newerp:must_change_password";

    /// <summary>
    /// SecurityStamp — версія облікового запису на момент входу. На кожному запиті її звіряють з базою
    /// (<see cref="AuthSetup"/>): після зміни ролі, пароля чи відкликання доступу стара cookie перестає діяти.
    /// </summary>
    public static ClaimsPrincipal ToPrincipal(this AuthenticatedUser authenticated)
    {
        var user = authenticated.User;
        return new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(FullName, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(SecurityStamp, authenticated.SecurityStamp),
                new Claim(MustChangePassword, user.MustChangePassword ? "1" : "0"),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static string? GetSecurityStamp(this ClaimsPrincipal principal) => principal.FindFirstValue(SecurityStamp);

    public static int GetUserId(this ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Користувач не увійшов у систему."), CultureInfo.InvariantCulture);

    /// <summary>Логін із dd_user.username. Старі журнали (dd_stock_log тощо) зберігають його поруч з id.</summary>
    public static string GetUsername(this ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.Name) ?? "";

    public static CurrentUser ToCurrentUser(this ClaimsPrincipal principal) => new(
        principal.GetUserId(),
        principal.FindFirstValue(ClaimTypes.Name) ?? "",
        principal.FindFirstValue(FullName) ?? "",
        principal.FindFirstValue(ClaimTypes.Role) ?? "",
        principal.FindFirstValue(MustChangePassword) == "1");
}
