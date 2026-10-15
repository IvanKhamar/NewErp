using System.Security.Cryptography;

using NewErp.Domain.Common;

namespace NewErp.Application.Auth;

/// <summary>Вимоги до пароля й генерація тимчасових паролів.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    // Без схожих символів (0/O, 1/l/I), щоб тимчасовий пароль легко було продиктувати чи переписати.
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static void Require(string? password, string username)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            throw new DomainException($"Пароль має містити щонайменше {MinLength} символів.");
        if (password.Length > MaxLength)
            throw new DomainException($"Пароль не довший за {MaxLength} символів.");
        if (string.Equals(password, username, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Пароль не може збігатися з логіном.");
    }

    /// <summary>Випадковий пароль із криптографічно стійкого генератора.</summary>
    public static string Generate(int length = 12) => RandomNumberGenerator.GetString(Alphabet, length);
}
