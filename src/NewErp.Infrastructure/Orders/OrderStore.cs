using Microsoft.EntityFrameworkCore;

using NewErp.Application.Orders;
using NewErp.Domain.Orders;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Orders;

public sealed class OrderStore(ErpDbContext db) : IOrderStore
{
    public async Task<int?> GetStatusIdAsync(int orderId, CancellationToken ct) =>
        await db.DdOrders
            .Where(o => o.OrderId == orderId)
            .Select(o => (int?)o.OrderStatusId)
            .FirstOrDefaultAsync(ct);

    public Task<bool> StatusExistsAsync(int statusId, CancellationToken ct) =>
        db.DdOrderStatuses.AnyAsync(s => s.OrderStatusId == statusId, ct);

    public Task<bool> IsActiveUserAsync(int userId, CancellationToken ct) =>
        db.DdUsers.AnyAsync(u => u.UserId == userId && u.Status, ct);

    public async Task SaveStatusChangeAsync(OrderStatusChange change, CancellationToken ct)
    {
        // Увага: таблиці OpenCart мають рушій MyISAM, який не підтримує транзакцій.
        // Тому оновлення й запис у журнал не атомарні: при збої між ними журнал може пропустити запис.
        var order = await db.DdOrders.FirstAsync(o => o.OrderId == change.OrderId, ct);
        order.OrderStatusId = change.NewStatusId;
        order.DateModified = change.ChangedAt;

        db.DdOrderStatusLogs.Add(new DdOrderStatusLog
        {
            OrderId = change.OrderId,
            UserId = change.UserId,
            OldStatusId = change.OldStatusId,
            NewStatusId = change.NewStatusId,
            DateAdded = change.ChangedAt,
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task SaveManagerAsync(int orderId, int managerId, int assignedByUserId, DateTime assignedAt, CancellationToken ct)
    {
        var row = await db.ErpOrderManagers.FindAsync([orderId], ct);
        if (row is null)
        {
            row = new ErpOrderManager { OrderId = orderId };
            db.ErpOrderManagers.Add(row);
        }

        row.UserId = managerId;
        row.AssignedByUserId = assignedByUserId;
        row.AssignedAt = assignedAt;

        await db.SaveChangesAsync(ct);
    }
}