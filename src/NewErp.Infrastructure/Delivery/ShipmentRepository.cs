using Microsoft.EntityFrameworkCore;

using NewErp.Application.Delivery;
using NewErp.Domain.Delivery;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Delivery;

public sealed class ShipmentRepository(ErpDbContext db) : IShipmentRepository
{
    public async Task<ShipmentDto?> GetForOrderAsync(int orderId, CancellationToken ct)
    {
        // Одна ТТН на замовлення; якщо їх кілька (перестворювали), показуємо останню, як і старий сайт.
        var document = await db.ShipmentDocuments.AsNoTracking()
            .Where(d => d.OrderId == orderId)
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync(ct);
        return document is null ? null : ToDto(document);
    }

    public async Task<IReadOnlyList<OpenShipment>> ListOpenForTrackingAsync(CancellationToken ct)
    {
        var finalCodes = ShipmentDocument.FinalStatusCodes.ToList();
        var rows = await (
            from d in db.ShipmentDocuments
            join o in db.DdOrders on d.OrderId equals o.OrderId
            where d.Number != "" && !finalCodes.Contains(d.StatusCode)
            select new { Document = d, o.Telephone }
        ).ToListAsync(ct);
        return rows.Select(r => new OpenShipment(r.Document, r.Telephone)).ToList();
    }

    public async Task<IReadOnlyList<ReminderDto>> ListRemindersAsync(DateOnly keepingFrom, DateOnly keepingTo, CancellationToken ct)
    {
        var waitingCodes = ShipmentDocument.WaitingAtBranchCodes.ToList();
        var rows = await (
            from d in db.ShipmentDocuments.AsNoTracking()
            join o in db.DdOrders on d.OrderId equals o.OrderId
            join m in db.ErpOrderManagers on o.OrderId equals m.OrderId into ms
            from m in ms.DefaultIfEmpty()
            join u in db.DdUsers on m.UserId equals u.UserId into us
            from u in us.DefaultIfEmpty()
            where waitingCodes.Contains(d.StatusCode)
                && d.DatePayedKeeping != null && d.DatePayedKeeping >= keepingFrom && d.DatePayedKeeping <= keepingTo
                && o.DateRemind == null
            orderby d.DatePayedKeeping
            select new
            {
                o.OrderId, o.Firstname, o.Lastname, o.Telephone, d.Number, d.Status, d.DatePayedKeeping,
                ManagerFirst = u == null ? null : u.Firstname, ManagerLast = u == null ? null : u.Lastname,
            }
        ).ToListAsync(ct);

        return rows
            .Select(r => new ReminderDto(
                r.OrderId, $"{r.Firstname} {r.Lastname}".Trim(), r.Telephone, r.Number, r.Status, r.DatePayedKeeping!.Value,
                r.ManagerFirst is null ? null : $"{r.ManagerFirst} {r.ManagerLast}".Trim()))
            .ToList();
    }

    public async Task<bool> MarkRemindedAsync(int orderId, DateTime remindedAt, CancellationToken ct) =>
        await db.DdOrders.Where(o => o.OrderId == orderId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.DateRemind, remindedAt), ct) > 0;

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static ShipmentDto ToDto(ShipmentDocument d) => new(
        d.Id, d.OrderId, d.Carrier, d.Number, d.Cost, d.Status, d.StatusCode, d.Tone, d.IsFinal, d.RecipientAddress, d.Redelivery,
        d.DateCreated, d.DateScheduled, d.DatePayedKeeping);
}
