using Microsoft.EntityFrameworkCore;

using NewErp.Application.Auth;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Auth;

public sealed class UserLoginStore(ErpDbContext db) : IUserLoginStore
{
    public Task<UserLoginRecord?> FindByUsernameAsync(string username, CancellationToken ct) =>
        (from u in db.DdUsers
         join l in db.UserLogins on u.UserId equals l.UserId
         where u.Username == username
         select new UserLoginRecord(u.UserId, u.Username, u.Firstname + " " + u.Lastname, u.Status, l.PasswordHash, l.Role))
        .FirstOrDefaultAsync(ct);

    public Task SetLastLoginAsync(int userId, DateTime loggedInAt, CancellationToken ct) =>
        db.UserLogins
            .Where(l => l.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.LastLoginAt, loggedInAt), ct);
}
