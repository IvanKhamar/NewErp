namespace NewErp.Application.Auth;

/// <summary>Користувачі (dd_user старого сайту) і їхній вхід у NewErp (erp_user_login).</summary>
public interface IUserLoginStore
{
    /// <summary>Повертає null, якщо такого логіна немає або для користувача не створено вхід у NewErp.</summary>
    Task<UserLoginRecord?> FindByUsernameAsync(string username, CancellationToken ct);

    Task<UserLoginRecord?> FindByIdAsync(int userId, CancellationToken ct);

    Task SetLastLoginAsync(int userId, DateTime loggedInAt, CancellationToken ct);

    /// <summary>Версія облікового запису, якщо вхід є і користувач активний на старому сайті; інакше null.</summary>
    Task<string?> GetActiveSecurityStampAsync(int userId, CancellationToken ct);

    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct);

    Task<UserDto?> GetAsync(int userId, CancellationToken ct);

    Task<bool> UsernameTakenAsync(string username, CancellationToken ct);

    /// <summary>Додає працівника в dd_user (без пароля старого сайту, тож у стару адмінку він не увійде).</summary>
    Task<int> CreateUserAsync(NewUser user, DateTime now, CancellationToken ct);

    /// <summary>
    /// Створює або змінює вхід. null у role, passwordHash чи mustChangePassword означає «не змінювати»
    /// (для нового входу роль і пароль обов'язкові). Щоразу видає нову версію облікового запису.
    /// </summary>
    /// <returns>Нова версія облікового запису.</returns>
    Task<string> SaveLoginAsync(int userId, string? role, string? passwordHash, bool? mustChangePassword, CancellationToken ct);

    /// <returns>false, якщо входу й так не було.</returns>
    Task<bool> DeleteLoginAsync(int userId, CancellationToken ct);
}
