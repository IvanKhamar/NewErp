using NewErp.Application.Auth;

namespace NewErp.Web.Auth;

/// <summary>
/// Які ролі бачать який розділ інтерфейсу. Ті самі правила, що в політиках API (NewErp.Api/Auth/AuthSetup.cs):
/// інтерфейс лише ховає недоступне, а справжню перевірку робить API.
/// </summary>
public static class Access
{
    public const string Stock = $"{Roles.Admin},{Roles.Storekeeper}";
    public const string Production = $"{Roles.Admin},{Roles.Storekeeper}";
    public const string Orders = $"{Roles.Admin},{Roles.Manager}";
    public const string Bonuses = Roles.Admin;
    public const string Users = Roles.Admin;
}
