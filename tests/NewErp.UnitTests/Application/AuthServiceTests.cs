using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Auth;

namespace NewErp.UnitTests.Application;

public class AuthServiceTests
{
    private readonly FakeUserLoginStore _store = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _store.Users.Add(new UserLoginRecord(1, "olena", "Олена Тест", IsActive: true, "hash:secret", Roles.Manager));
        _store.Users.Add(new UserLoginRecord(2, "ihor", "Ігор Тест", IsActive: false, "hash:secret", Roles.Manager));
        _service = new AuthService(_store, new FakeHasher(), new FakeTimeProvider());
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsUserAndRecordsTime()
    {
        var user = await _service.LoginAsync(new LoginRequest(" olena ", "secret"), CancellationToken.None);

        Assert.Equal(new CurrentUser(1, "olena", "Олена Тест", Roles.Manager), user);
        Assert.Equal([1], _store.LoggedIn);
    }

    [Theory]
    [InlineData("olena", "wrong")]   // невірний пароль
    [InlineData("ihor", "secret")]   // користувача вимкнено
    [InlineData("nobody", "secret")] // немає такого логіна
    [InlineData("", "")]
    public async Task Login_Rejected_ReturnsNullAndRecordsNothing(string username, string password)
    {
        Assert.Null(await _service.LoginAsync(new LoginRequest(username, password), CancellationToken.None));
        Assert.Empty(_store.LoggedIn);
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;

        public bool Verify(string passwordHash, string password) => passwordHash == Hash(password);
    }

    private sealed class FakeUserLoginStore : IUserLoginStore
    {
        public List<UserLoginRecord> Users { get; } = [];

        public List<int> LoggedIn { get; } = [];

        public Task<UserLoginRecord?> FindByUsernameAsync(string username, CancellationToken ct) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Username == username));

        public Task SetLastLoginAsync(int userId, DateTime loggedInAt, CancellationToken ct)
        {
            LoggedIn.Add(userId);
            return Task.CompletedTask;
        }
    }
}
