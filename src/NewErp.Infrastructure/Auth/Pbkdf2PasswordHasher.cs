using Microsoft.AspNetCore.Identity;

using NewErp.Application.Auth;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Auth;

/// <summary>
/// Обгортка над PasswordHasher з ASP.NET Core Identity. Він додає до пароля випадкову сіль
/// і хешує його сотні тисяч разів (PBKDF2), тож підбирати паролі за хешем дуже повільно.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private static readonly ErpUserLogin Unused = new(); // PasswordHasher вимагає об'єкт користувача, але не використовує його

    private readonly PasswordHasher<ErpUserLogin> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(Unused, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(Unused, passwordHash, password) != PasswordVerificationResult.Failed;
}
