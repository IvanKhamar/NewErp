namespace NewErp.Application.Auth;

public sealed record LoginRequest(string Username, string Password);

/// <summary>Користувач, який увійшов у систему. MustChangePassword: пароль тимчасовий, його треба змінити.</summary>
public sealed record CurrentUser(int UserId, string Username, string FullName, string Role, bool MustChangePassword = false);

/// <summary>Результат входу: користувач і версія облікового запису, яка кладеться в cookie.</summary>
public sealed record AuthenticatedUser(CurrentUser User, string SecurityStamp);

/// <summary>Обліковий запис для перевірки входу: дані з dd_user плюс хеш пароля й роль з erp_user_login.</summary>
public sealed record UserLoginRecord(
    int UserId, string Username, string FullName, bool IsActive, string PasswordHash, string Role, string SecurityStamp, bool MustChangePassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// ── Керування користувачами (Admin) ──

/// <summary>
/// HasAccess: чи може входити в NewErp. ActiveOnOldSite — dd_user.status: вимкнений на старому сайті не входить і в NewErp.
/// </summary>
public sealed record UserDto(
    int UserId, string Username, string FullName, string Email, bool ActiveOnOldSite, bool HasAccess, string? Role, DateTime? LastLoginAt);

/// <summary>TemporaryPassword не вказано — система згенерує сама й поверне його один раз.</summary>
public sealed record CreateUserRequest(string Username, string FirstName, string LastName, string? Email, string Role, string? TemporaryPassword = null);

public sealed record SetAccessRequest(string Role, string? TemporaryPassword = null);

public sealed record ResetPasswordRequest(string? TemporaryPassword = null);

/// <summary>TemporaryPassword показується лише тут, один раз; його треба передати людині, а вона змінить його після входу.</summary>
public sealed record UserAccessResult(int UserId, string? TemporaryPassword);

/// <summary>Новий працівник для dd_user.</summary>
public sealed record NewUser(string Username, string FirstName, string LastName, string Email);
