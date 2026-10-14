namespace NewErp.Application.Auth;

/// <summary>Хешування паролів. Сам пароль ніде не зберігається, лише його хеш.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
