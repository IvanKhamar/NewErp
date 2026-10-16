using Microsoft.EntityFrameworkCore;

using NewErp.Application.Sms;
using NewErp.Domain.Sms;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Sms;

public sealed class SmsLog(ErpDbContext db) : ISmsLog
{
    public Task<string?> GetOrderPhoneAsync(int orderId, CancellationToken ct) =>
        db.DdOrders.Where(o => o.OrderId == orderId).Select(o => o.Telephone).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<SmsDto>> ListForOrderAsync(int orderId, CancellationToken ct) =>
        await db.SmsMessages.AsNoTracking()
            .Where(m => m.OrderId == orderId)
            .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.Id)
            .Select(m => new SmsDto(m.Id, m.OrderId, m.Phone, m.Text, m.Response, m.SentAt))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, int>> FindOrderIdsByTtnAsync(IReadOnlyCollection<string> numbers, CancellationToken ct)
    {
        if (numbers.Count == 0)
            return new Dictionary<string, int>();

        var rows = await db.ShipmentDocuments.AsNoTracking()
            .Where(d => numbers.Contains(d.Number))
            .OrderByDescending(d => d.Id) // як старий сайт: якщо номер повторюється, береться останній запис
            .Select(d => new { d.Number, d.OrderId })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.Number).ToDictionary(g => g.Key, g => g.First().OrderId);
    }

    public void Add(SmsMessage message) => db.SmsMessages.Add(message);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
