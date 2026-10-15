using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Auth;
using NewErp.Application.Common;
using NewErp.Domain.Common;

namespace NewErp.UnitTests.Application;

public class AuthServiceTests
{
    private readonly FakeUserLoginStore _store = new();
    private readonly AuthService _service;
    private readonly UserAdminService _admin;

    public AuthServiceTests()
    {
        _store.Add(1, "olena", "Олена Тест", active: true, "hash:secret-pass", Roles.Admin);
        _store.Add(2, "ihor", "Ігор Тест", active: false, "hash:secret-pass", Roles.Manager);
        _store.Add(3, "marta", "Марта Тест", active: true, passwordHash: null, role: null); // є на старому сайті, доступу немає
        _service = new AuthService(_store, new FakeHasher(), new FakeTimeProvider());
        _admin = new UserAdminService(_store, new FakeHasher(), new FakeTimeProvider());
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsUserWithStampAndRecordsTime()
    {
        var result = await _service.LoginAsync(new LoginRequest(" olena ", "secret-pass"), CancellationToken.None);

        Assert.Equal(new CurrentUser(1, "olena", "Олена Тест", Roles.Admin), result!.User);
        Assert.Equal(_store.Stamp(1), result.SecurityStamp);
        Assert.Equal([1], _store.LoggedIn);
    }

    [Theory]
    [InlineData("olena", "wrong")]        // невірний пароль
    [InlineData("ihor", "secret-pass")]   // вимкнено на старому сайті
    [InlineData("marta", "")]             // немає доступу до NewErp
    [InlineData("nobody", "secret-pass")] // немає такого логіна
    [InlineData("", "")]
    public async Task Login_Rejected_ReturnsNullAndRecordsNothing(string username, string password)
    {
        Assert.Null(await _service.LoginAsync(new LoginRequest(username, password), CancellationToken.None));
        Assert.Empty(_store.LoggedIn);
    }

    [Fact]
    public async Task ChangePassword_NeedsCurrentAndChangesStamp()
    {
        var before = _store.Stamp(1);

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.ChangePasswordAsync(1, new ChangePasswordRequest("wrong", "new-password-1"), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.ChangePasswordAsync(1, new ChangePasswordRequest("secret-pass", "short"), CancellationToken.None));

        var result = await _service.ChangePasswordAsync(1, new ChangePasswordRequest("secret-pass", "new-password-1"), CancellationToken.None);

        Assert.NotEqual(before, result.SecurityStamp);
        Assert.NotNull(await _service.LoginAsync(new LoginRequest("olena", "new-password-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Create_NewWorkerGetsTemporaryPasswordAndMustChangeIt()
    {
        var result = await _admin.CreateAsync(new CreateUserRequest("sklad2", "Петро", "Новий", null, "storekeeper"), CancellationToken.None);

        Assert.Equal(12, result.TemporaryPassword!.Length);
        var login = await _service.LoginAsync(new LoginRequest("sklad2", result.TemporaryPassword), CancellationToken.None);
        Assert.Equal((Roles.Storekeeper, true), (login!.User.Role, login.User.MustChangePassword));
    }

    [Fact]
    public async Task Create_TakenUsernameOrUnknownRole_Rejected()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _admin.CreateAsync(new CreateUserRequest("olena", "Олена", "Друга", null, Roles.Manager), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() =>
            _admin.CreateAsync(new CreateUserRequest("new1", "Нова", "Людина", null, "Boss"), CancellationToken.None));
    }

    [Fact]
    public async Task SetAccess_ExistingOldSiteUserGetsPassword_RoleChangeKeepsPassword()
    {
        var granted = await _admin.SetAccessAsync(actorId: 1, 3, new SetAccessRequest(Roles.Manager), CancellationToken.None);
        Assert.NotNull(granted.TemporaryPassword);

        var stamp = _store.Stamp(3);
        var changed = await _admin.SetAccessAsync(actorId: 1, 3, new SetAccessRequest(Roles.Storekeeper), CancellationToken.None);

        Assert.Null(changed.TemporaryPassword); // пароль не чіпали
        Assert.NotEqual(stamp, _store.Stamp(3)); // але старі сесії з роллю Manager більше не діють
        Assert.NotNull(await _service.LoginAsync(new LoginRequest("marta", granted.TemporaryPassword!), CancellationToken.None));
    }

    [Fact]
    public async Task Admin_CannotDemoteOrRevokeSelf()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _admin.SetAccessAsync(actorId: 1, 1, new SetAccessRequest(Roles.Manager), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => _admin.RevokeAsync(actorId: 1, 1, CancellationToken.None));
    }

    [Fact]
    public async Task Revoke_RemovesAccess()
    {
        await _admin.RevokeAsync(actorId: 1, 2, CancellationToken.None);

        Assert.Null(await _store.GetActiveSecurityStampAsync(2, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _admin.RevokeAsync(actorId: 1, 2, CancellationToken.None));
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;

        public bool Verify(string passwordHash, string password) => passwordHash == Hash(password);
    }

    /// <summary>Користувачі в пам'яті: рядок dd_user і, якщо є, вхід у NewErp.</summary>
    private sealed class FakeUserLoginStore : IUserLoginStore
    {
        private sealed class Row
        {
            public required int Id;
            public required string Username;
            public required string FullName;
            public required bool Active;
            public string? Hash;
            public string? Role;
            public string Stamp = Guid.NewGuid().ToString("N");
            public bool MustChange;
        }

        private readonly List<Row> _rows = [];

        public List<int> LoggedIn { get; } = [];

        public void Add(int id, string username, string fullName, bool active, string? passwordHash, string? role) =>
            _rows.Add(new Row { Id = id, Username = username, FullName = fullName, Active = active, Hash = passwordHash, Role = role });

        public string Stamp(int id) => _rows.Single(r => r.Id == id).Stamp;

        public Task<UserLoginRecord?> FindByUsernameAsync(string username, CancellationToken ct) =>
            Task.FromResult(Record(_rows.FirstOrDefault(r => r.Username == username)));

        public Task<UserLoginRecord?> FindByIdAsync(int userId, CancellationToken ct) =>
            Task.FromResult(Record(_rows.FirstOrDefault(r => r.Id == userId)));

        public Task SetLastLoginAsync(int userId, DateTime loggedInAt, CancellationToken ct)
        {
            LoggedIn.Add(userId);
            return Task.CompletedTask;
        }

        public Task<string?> GetActiveSecurityStampAsync(int userId, CancellationToken ct) =>
            Task.FromResult(_rows.FirstOrDefault(r => r.Id == userId && r.Active && r.Hash != null)?.Stamp);

        public Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UserDto>>(_rows.Select(Dto).ToList());

        public Task<UserDto?> GetAsync(int userId, CancellationToken ct) =>
            Task.FromResult(_rows.Where(r => r.Id == userId).Select(Dto).FirstOrDefault());

        public Task<bool> UsernameTakenAsync(string username, CancellationToken ct) =>
            Task.FromResult(_rows.Any(r => string.Equals(r.Username, username, StringComparison.OrdinalIgnoreCase)));

        public Task<int> CreateUserAsync(NewUser user, DateTime now, CancellationToken ct)
        {
            var id = _rows.Max(r => r.Id) + 1;
            Add(id, user.Username, $"{user.FirstName} {user.LastName}", active: true, passwordHash: null, role: null);
            return Task.FromResult(id);
        }

        public Task<string> SaveLoginAsync(int userId, string? role, string? passwordHash, bool? mustChangePassword, CancellationToken ct)
        {
            var row = _rows.Single(r => r.Id == userId);
            row.Role = role ?? row.Role;
            row.Hash = passwordHash ?? row.Hash;
            row.MustChange = mustChangePassword ?? row.MustChange;
            row.Stamp = Guid.NewGuid().ToString("N");
            return Task.FromResult(row.Stamp);
        }

        public Task<bool> DeleteLoginAsync(int userId, CancellationToken ct)
        {
            var row = _rows.Single(r => r.Id == userId);
            var had = row.Hash != null;
            (row.Hash, row.Role) = (null, null);
            return Task.FromResult(had);
        }

        private static UserLoginRecord? Record(Row? r) =>
            r is { Hash: not null, Role: not null }
                ? new UserLoginRecord(r.Id, r.Username, r.FullName, r.Active, r.Hash, r.Role, r.Stamp, r.MustChange)
                : null;

        private static UserDto Dto(Row r) => new(r.Id, r.Username, r.FullName, "", r.Active, r.Hash != null, r.Role, null);
    }
}
