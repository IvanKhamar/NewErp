namespace NewErp.Infrastructure.Persistence;

/// <summary>Рядок erp_user_login: хеш пароля й роль користувача з dd_user.</summary>
public sealed class ErpUserLogin
{
    public int UserId { get; set; }

    public string PasswordHash { get; set; } = "";

    public string Role { get; set; } = "";

    public DateTime? LastLoginAt { get; set; }
}
