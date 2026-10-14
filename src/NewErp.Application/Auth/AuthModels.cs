namespace NewErp.Application.Auth;

public sealed record LoginRequest(string Username, string Password);

/// <summary>Користувач, який увійшов у систему.</summary>
public sealed record CurrentUser(int UserId, string Username, string FullName, string Role);

/// <summary>Обліковий запис для перевірки входу: дані з dd_user плюс хеш пароля й роль з erp_user_login.</summary>
public sealed record UserLoginRecord(int UserId, string Username, string FullName, bool IsActive, string PasswordHash, string Role);
