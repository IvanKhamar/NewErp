using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

using NewErp.Application.Auth;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Auth;

public sealed class UserLoginStore(ErpDbContext db) : IUserLoginStore
{
    public Task<UserLoginRecord?> FindByUsernameAsync(string username, CancellationToken ct) =>
        Records(u => u.Username == username).FirstOrDefaultAsync(ct);

    public Task<UserLoginRecord?> FindByIdAsync(int userId, CancellationToken ct) =>
        Records(u => u.UserId == userId).FirstOrDefaultAsync(ct);

    public Task SetLastLoginAsync(int userId, DateTime loggedInAt, CancellationToken ct) =>
        db.UserLogins
            .Where(l => l.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.LastLoginAt, loggedInAt), ct);

    public Task<string?> GetActiveSecurityStampAsync(int userId, CancellationToken ct) =>
        (from u in db.DdUsers
         join l in db.UserLogins on u.UserId equals l.UserId
         where u.UserId == userId && u.Status
         select l.SecurityStamp)
        .FirstOrDefaultAsync(ct);

    /// <summary>Спершу ті, хто має доступ до NewErp, далі за іменем.</summary>
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct) =>
        (await Users(_ => true).ToListAsync(ct))
            .OrderByDescending(u => u.HasAccess).ThenBy(u => u.FullName, StringComparer.CurrentCulture)
            .ToList();

    public Task<UserDto?> GetAsync(int userId, CancellationToken ct) =>
        Users(u => u.UserId == userId).FirstOrDefaultAsync(ct);

    public Task<bool> UsernameTakenAsync(string username, CancellationToken ct) =>
        db.DdUsers.AnyAsync(u => u.Username == username, ct);

    public async Task<int> CreateUserAsync(NewUser user, DateTime now, CancellationToken ct)
    {
        // Порожні password і salt: такий хеш на старому сайті не збігається з жодним паролем, тож у стару адмінку
        // людина не увійде. user_group_id = 0 — жодних прав у старій адмінці.
        var row = new DdUser
        {
            UserGroupId = 0,
            Username = user.Username,
            Password = "",
            Salt = "",
            Firstname = user.FirstName,
            Lastname = user.LastName,
            Email = user.Email,
            Image = "",
            Code = "",
            Ip = "",
            Status = true,
            DateAdded = now,
        };
        db.DdUsers.Add(row);
        await db.SaveChangesAsync(ct);
        return row.UserId;
    }

    public async Task<string> SaveLoginAsync(int userId, string? role, string? passwordHash, bool? mustChangePassword, CancellationToken ct)
    {
        var login = await db.UserLogins.FirstOrDefaultAsync(l => l.UserId == userId, ct);
        if (login is null)
        {
            if (role is null || passwordHash is null)
                throw new InvalidOperationException("Для нового входу потрібні роль і пароль.");
            login = new ErpUserLogin { UserId = userId };
            db.UserLogins.Add(login);
        }

        login.Role = role ?? login.Role;
        login.PasswordHash = passwordHash ?? login.PasswordHash;
        login.MustChangePassword = mustChangePassword ?? login.MustChangePassword;
        login.SecurityStamp = ErpUserLogin.NewStamp();
        await db.SaveChangesAsync(ct);
        return login.SecurityStamp;
    }

    public async Task<bool> DeleteLoginAsync(int userId, CancellationToken ct) =>
        await db.UserLogins.Where(l => l.UserId == userId).ExecuteDeleteAsync(ct) > 0;

    // Фільтр застосовується до dd_user ще до проєкції в record: умову на полях record EF у SQL не перекладе.
    private IQueryable<UserLoginRecord> Records(Expression<Func<DdUser, bool>> filter) =>
        from u in db.DdUsers.Where(filter)
        join l in db.UserLogins on u.UserId equals l.UserId
        select new UserLoginRecord(
            u.UserId, u.Username, u.Firstname + " " + u.Lastname, u.Status, l.PasswordHash, l.Role, l.SecurityStamp, l.MustChangePassword);

    private IQueryable<UserDto> Users(Expression<Func<DdUser, bool>> filter) =>
        from u in db.DdUsers.Where(filter)
        join l in db.UserLogins on u.UserId equals l.UserId into ls
        from l in ls.DefaultIfEmpty()
        select new UserDto(
            u.UserId, u.Username, u.Firstname + " " + u.Lastname, u.Email, u.Status,
            l != null, l == null ? null : l.Role, l == null ? null : l.LastLoginAt);
}
