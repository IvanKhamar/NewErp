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

    public Task SaveShippingDateAsync(int orderId, DateOnly? day, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dd_order SET date_shipping = {day?.ToDateTime(TimeOnly.MinValue)} WHERE order_id = {orderId}", ct);

    public async Task<string> CreateInvoiceNoAsync(int orderId, CancellationToken ct)
    {
        var prefix = await db.DdOrders.Where(o => o.OrderId == orderId).Select(o => o.InvoicePrefix).FirstAsync(ct);
        // Одна інструкція, тож два натискання водночас не візьмуть той самий номер (MyISAM блокує для неї таблицю).
        // Підзапит до тієї самої таблиці MySQL не дозволяє в UPDATE, тому похідна таблиця t.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE dd_order SET invoice_no = (SELECT n FROM (SELECT COALESCE(MAX(invoice_no), 0) + 1 AS n FROM dd_order
            WHERE invoice_prefix = {prefix}) t) WHERE order_id = {orderId} AND invoice_no = 0
            """, ct);
        var number = await db.DdOrders.AsNoTracking().Where(o => o.OrderId == orderId).Select(o => o.InvoiceNo).FirstAsync(ct);
        return $"{prefix}{number}";
    }

    public async Task<IReadOnlyList<string>> DeleteBlockersAsync(int orderId, CancellationToken ct)
    {
        var blockers = new List<string>();
        if (await db.ShipmentDocuments.AnyAsync(d => d.OrderId == orderId && d.Number != "", ct))
            blockers.Add("є ТТН");
        if (await db.Cheques.AnyAsync(c => c.OrderId == orderId, ct))
            blockers.Add("є фіскальний чек");
        if (await db.BankPayments.AnyAsync(p => p.OrderId == orderId, ct))
            blockers.Add("до нього прив'язана банківська оплата");
        if (await db.DdReturns.AnyAsync(r => r.OrderId == orderId, ct))
            blockers.Add("є повернення");
        if (await db.DdVouchers.AnyAsync(v => v.OrderId == orderId, ct))
            blockers.Add("у ньому куплено подарунковий сертифікат");
        if (await db.DdOrderTotals.AnyAsync(t => t.OrderId == orderId && t.Code == "voucher", ct))
            blockers.Add("його оплачено сертифікатом");
        return blockers;
    }

    public async Task DeleteAsync(int orderId, CancellationToken ct)
    {
        // MyISAM: кожне видалення окреме. Рядок замовлення — останнім, тож після збою замовлення ще є
        // (без частини рядків), і його можна видалити знову.
        await db.DdOrderOptions.Where(x => x.OrderId == orderId).ExecuteDeleteAsync(ct);
        await db.DdOrderProducts.Where(x => x.OrderId == orderId).ExecuteDeleteAsync(ct);
        await db.DdOrderTotals.Where(x => x.OrderId == orderId).ExecuteDeleteAsync(ct);
        await db.ErpOrderManagers.Where(x => x.OrderId == orderId).ExecuteDeleteAsync(ct);
        await db.CustomerRewards.Where(x => x.OrderId == orderId).ExecuteDeleteAsync(ct);
        await db.CustomerTransactions.Where(x => x.OrderId == orderId).ExecuteDeleteAsync(ct);
        await db.DdOrders.Where(x => x.OrderId == orderId).ExecuteDeleteAsync(ct);
    }
}
