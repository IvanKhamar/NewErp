namespace NewErp.Application.Auth;

public interface IUserLoginStore
{
    /// <summary>Повертає null, якщо такого логіна немає або для користувача не створено вхід у NewErp.</summary>
    Task<UserLoginRecord?> FindByUsernameAsync(string username, CancellationToken ct);

    Task SetLastLoginAsync(int userId, DateTime loggedInAt, CancellationToken ct);
}
