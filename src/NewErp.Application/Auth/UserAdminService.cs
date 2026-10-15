using NewErp.Application.Common;
using NewErp.Domain.Common;

namespace NewErp.Application.Auth;

/// <summary>
/// Керування користувачами (лише Admin): нові працівники, доступ до NewErp, ролі, скидання паролів.
/// Будь-яка зміна видає нову версію облікового запису, тож старі сесії цієї людини одразу перестають діяти.
/// </summary>
public sealed class UserAdminService(IUserLoginStore store, IPasswordHasher hasher, TimeProvider clock)
{
    public Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct) => store.ListAsync(ct);

    /// <summary>Новий працівник: рядок у dd_user (спільний зі старим сайтом) і вхід у NewErp з тимчасовим паролем.</summary>
    public async Task<UserAccessResult> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var username = RequireText(request.Username, "Логін", 3, 20);
        var user = new NewUser(
            username,
            RequireText(request.FirstName, "Ім'я", 1, 32),
            RequireText(request.LastName, "Прізвище", 1, 32),
            request.Email?.Trim() ?? "");
        if (user.Email.Length > 96)
            throw new DomainException("Email: не довше 96 символів.");
        var role = RequireRole(request.Role);
        if (await store.UsernameTakenAsync(username, ct))
            throw new DomainException($"Логін \"{username}\" вже зайнятий.");
        var password = TemporaryPassword(request.TemporaryPassword, username);

        var userId = await store.CreateUserAsync(user, clock.GetLocalNow().DateTime, ct);
        await store.SaveLoginAsync(userId, role, hasher.Hash(password), mustChangePassword: true, ct);
        return new UserAccessResult(userId, password);
    }

    /// <summary>
    /// Видає доступ наявному користувачу або змінює роль. Для нового доступу потрібен пароль
    /// (якщо не вказано — згенерується); при зміні ролі пароль не чіпаємо, якщо його не передали.
    /// </summary>
    public async Task<UserAccessResult> SetAccessAsync(int actorId, int userId, SetAccessRequest request, CancellationToken ct)
    {
        var user = await store.GetAsync(userId, ct) ?? throw new NotFoundException($"Користувача {userId} не знайдено.");
        var role = RequireRole(request.Role);
        if (userId == actorId && role != Roles.Admin)
            throw new DomainException("Не можна зняти роль Admin із себе: система може лишитися без адміністратора.");

        string? password = null;
        if (!user.HasAccess || !string.IsNullOrEmpty(request.TemporaryPassword))
            password = TemporaryPassword(request.TemporaryPassword, user.Username);

        await store.SaveLoginAsync(userId, role, password is null ? null : hasher.Hash(password), password is null ? null : true, ct);
        return new UserAccessResult(userId, password);
    }

    /// <summary>Забирає доступ до NewErp. На старому сайті користувач лишається, як був.</summary>
    public async Task RevokeAsync(int actorId, int userId, CancellationToken ct)
    {
        if (userId == actorId)
            throw new DomainException("Не можна забрати доступ у самого себе.");
        if (!await store.DeleteLoginAsync(userId, ct))
            throw new NotFoundException($"У користувача {userId} немає доступу до NewErp.");
    }

    /// <summary>Скидає пароль на тимчасовий; людина змінить його сама після входу.</summary>
    public async Task<UserAccessResult> ResetPasswordAsync(int userId, ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await store.GetAsync(userId, ct);
        if (user is not { HasAccess: true })
            throw new NotFoundException($"У користувача {userId} немає доступу до NewErp.");

        var password = TemporaryPassword(request.TemporaryPassword, user.Username);
        await store.SaveLoginAsync(userId, role: null, hasher.Hash(password), mustChangePassword: true, ct);
        return new UserAccessResult(userId, password);
    }

    private static string TemporaryPassword(string? requested, string username)
    {
        if (string.IsNullOrEmpty(requested))
            return PasswordPolicy.Generate();
        PasswordPolicy.Require(requested, username);
        return requested;
    }

    private static string RequireRole(string? role) =>
        Roles.All.FirstOrDefault(r => string.Equals(r, role?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException($"Роль має бути однією з: {string.Join(", ", Roles.All)}.");

    private static string RequireText(string? value, string field, int min, int max)
    {
        var text = value?.Trim() ?? "";
        if (text.Length < min || text.Length > max)
            throw new DomainException($"{field}: від {min} до {max} символів.");
        return text;
    }
}
