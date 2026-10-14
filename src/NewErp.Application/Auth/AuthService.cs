namespace NewErp.Application.Auth;

/// <summary>Перевірка логіна й пароля.</summary>
public sealed class AuthService(IUserLoginStore store, IPasswordHasher hasher, TimeProvider clock)
{
    /// <summary>
    /// Повертає користувача, якщо логін і пароль правильні й обліковий запис активний, інакше null.
    /// Причину відмови навмисно не уточнюємо, щоб не підказувати, які логіни існують.
    /// </summary>
    public async Task<CurrentUser?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return null;

        var user = await store.FindByUsernameAsync(request.Username.Trim(), ct);
        if (user is null || !user.IsActive || !hasher.Verify(user.PasswordHash, request.Password))
            return null;

        await store.SetLastLoginAsync(user.UserId, clock.GetLocalNow().DateTime, ct);
        return new CurrentUser(user.UserId, user.Username, user.FullName, user.Role);
    }
}
