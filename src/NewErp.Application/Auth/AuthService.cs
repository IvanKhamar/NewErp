using NewErp.Domain.Common;

namespace NewErp.Application.Auth;

/// <summary>Вхід і зміна власного пароля.</summary>
public sealed class AuthService(IUserLoginStore store, IPasswordHasher hasher, TimeProvider clock)
{
    /// <summary>
    /// Повертає користувача, якщо логін і пароль правильні й обліковий запис активний, інакше null.
    /// Причину відмови навмисно не уточнюємо, щоб не підказувати, які логіни існують.
    /// </summary>
    public async Task<AuthenticatedUser?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return null;

        var user = await store.FindByUsernameAsync(request.Username.Trim(), ct);
        if (user is null || !user.IsActive || !hasher.Verify(user.PasswordHash, request.Password))
            return null;

        await store.SetLastLoginAsync(user.UserId, clock.GetLocalNow().DateTime, ct);
        return new AuthenticatedUser(
            new CurrentUser(user.UserId, user.Username, user.FullName, user.Role, user.MustChangePassword), user.SecurityStamp);
    }

    /// <summary>
    /// Змінює власний пароль (потрібен поточний). Знімає позначку «тимчасовий пароль».
    /// Нова версія облікового запису виходить з усіх інших сесій цього користувача.
    /// </summary>
    public async Task<AuthenticatedUser> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await store.FindByIdAsync(userId, ct)
            ?? throw new DomainException("Обліковий запис не знайдено.");
        if (!hasher.Verify(user.PasswordHash, request.CurrentPassword ?? ""))
            throw new DomainException("Поточний пароль неправильний.");
        if (request.NewPassword == request.CurrentPassword)
            throw new DomainException("Новий пароль має відрізнятися від поточного.");
        PasswordPolicy.Require(request.NewPassword, user.Username);

        var stamp = await store.SaveLoginAsync(userId, role: null, hasher.Hash(request.NewPassword), mustChangePassword: false, ct);
        return new AuthenticatedUser(new CurrentUser(user.UserId, user.Username, user.FullName, user.Role), stamp);
    }
}
