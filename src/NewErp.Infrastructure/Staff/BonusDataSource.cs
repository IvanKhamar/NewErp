using Microsoft.EntityFrameworkCore;

using NewErp.Application.Staff;
using NewErp.Domain.Staff;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Staff;

public sealed class BonusDataSource(ErpDbContext db) : IBonusDataSource
{
    public async Task<IReadOnlyList<BonusOrder>> GetManagerOrdersAsync(DateTime start, DateTime endExclusive, CancellationToken ct) =>
        await (
            from o in db.DdOrders
            join m in db.ErpOrderManagers on o.OrderId equals m.OrderId
            where o.DateAdded >= start && o.DateAdded < endExclusive
            select new BonusOrder(o.OrderId, m.UserId, o.OrderStatusId, o.Total))
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, string>> GetUserNamesAsync(IEnumerable<int> userIds, CancellationToken ct)
    {
        var ids = userIds.ToList();
        return await db.DdUsers.AsNoTracking()
            .Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => $"{u.Firstname} {u.Lastname}", ct);
    }
}