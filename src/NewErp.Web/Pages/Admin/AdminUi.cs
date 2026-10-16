using NewErp.Application.Auth;

namespace NewErp.Web.Pages.Admin;

/// <summary>Підписи ролей в інтерфейсі.</summary>
public static class AdminUi
{
    public static string RoleLabel(string? role) => role switch
    {
        Roles.Admin => "Адміністратор",
        Roles.Manager => "Менеджер",
        Roles.Storekeeper => "Комірник",
        null => "—",
        _ => role,
    };
}
