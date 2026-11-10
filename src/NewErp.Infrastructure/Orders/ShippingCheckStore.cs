using Microsoft.EntityFrameworkCore;

using NewErp.Application.Orders;
using NewErp.Domain.Orders;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Orders;

/// <summary>Що читає перевірка відправки: замовлення, його остання ТТН, журнал SMS і журнал статусів.</summary>
public sealed class ShippingCheckStore(ErpDbContext db) : IShippingCheckStore
{
    /// <summary>Нульові чи порожні дати ТТН не враховуються, як і в старому запиті.</summary>
    private static readonly DateTime NoDateBelow = new(2000, 1, 1);

    public async Task<OrderForCheck?> OrderForCheckAsync(int orderId, CancellationToken ct)
    {
        var row = await db.DdOrders.AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => new { o.Firstname, o.Lastname, o.Telephone, o.ShippingCode, o.ShippingMethod, o.DateAdded })
            .FirstOrDefaultAsync(ct);
        return row is null ? null
            : new OrderForCheck($"{row.Firstname} {row.Lastname}".Trim(), row.Telephone, row.ShippingCode, row.ShippingMethod, row.DateAdded);
    }

    public async Task<string> LastWaybillAsync(int orderId, CancellationToken ct)
    {
        var number = await db.ShipmentDocuments.AsNoTracking()
            .Where(d => d.OrderId == orderId && d.Number != "")
            .OrderByDescending(d => d.Id)
            .Select(d => d.Number)
            .FirstOrDefaultAsync(ct);
        return ShippingChecks.WaybillDigits(number);
    }

    public async Task<IReadOnlyList<string>> SmsPhonesWithAsync(string waybill, DateTime since, CancellationToken ct)
    {
        // Номер — лише цифри, тож у LIKE його не треба екранувати; він однаково параметр запиту.
        var digits = ShippingChecks.WaybillDigits(waybill);
        return await db.SmsMessages.AsNoTracking()
            .Where(s => s.SentAt >= since && s.Text.Replace(" ", "").Contains(digits))
            .Select(s => s.Phone)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<(int OrderId, string Customer, DateTime Created)>> StaleWaybillsAsync(
        IReadOnlyList<int> statusIds, IReadOnlyList<string> shippingCodes, DateTime createdBefore, CancellationToken ct)
    {
        var rows = await (
                from o in db.DdOrders.AsNoTracking()
                join d in db.ShipmentDocuments.AsNoTracking() on o.OrderId equals d.OrderId
                where d.Number != "" && d.DateCreated > NoDateBelow
                    && statusIds.Contains(o.OrderStatusId) && shippingCodes.Contains(o.ShippingCode)
                group d.DateCreated by new { o.OrderId, o.Firstname, o.Lastname } into g
                select new { g.Key.OrderId, g.Key.Firstname, g.Key.Lastname, Created = g.Max() })
            .Where(r => r.Created <= createdBefore)
            .OrderBy(r => r.OrderId)
            .ToListAsync(ct);
        return rows.Select(r => (r.OrderId, $"{r.Firstname} {r.Lastname}".Trim(), r.Created!.Value)).ToList();
    }

    public async Task<IReadOnlyList<int>> OrdersMovedIntoAsync(int statusId, DateTime since, IReadOnlyList<string>? shippingCodes, CancellationToken ct)
    {
        var query = db.DdOrders.AsNoTracking()
            .Where(o => o.OrderStatusId == statusId
                && db.DdOrderStatusLogs.Any(l => l.OrderId == o.OrderId && l.NewStatusId == statusId && l.DateAdded >= since));
        if (shippingCodes is not null)
            query = query.Where(o => shippingCodes.Contains(o.ShippingCode));
        return await query.OrderBy(o => o.OrderId).Select(o => o.OrderId).ToListAsync(ct);
    }
}
