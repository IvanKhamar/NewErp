using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Common;
using NewErp.Application.Marketing;
using NewErp.Domain.Marketing;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Marketing;

/// <summary>Таблиці сертифікатів OpenCart (MyISAM, без транзакцій) — ті самі рядки, які сайт читає на оформленні.</summary>
public sealed class VoucherStore(ErpDbContext db, IOptions<ErpOptions> options) : IVoucherStore
{
    private const int MaxRows = 500;
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<IReadOnlyList<VoucherRow>> ListAsync(string search, CancellationToken ct) =>
        await Rows(db.DdVouchers
                .Where(v => search == "" || v.Code.Contains(search) || v.ToName.Contains(search) || v.FromName.Contains(search))
                .OrderByDescending(v => v.VoucherId)
                .Take(MaxRows))
            .ToListAsync(ct);

    public Task<VoucherRow?> FindAsync(int id, CancellationToken ct) => Rows(db.DdVouchers.Where(v => v.VoucherId == id)).FirstOrDefaultAsync(ct);

    public Task<VoucherRow?> FindByCodeAsync(string code, CancellationToken ct) =>
        Rows(db.DdVouchers.Where(v => v.Code == code).OrderByDescending(v => v.VoucherId)).FirstOrDefaultAsync(ct);

    public async Task<OrderSums?> OrderSumsAsync(int orderId, CancellationToken ct) =>
        await db.DdOrders.AsNoTracking()
            .Where(o => o.OrderId == orderId && o.OrderStatusId > 0)
            .Select(o => new OrderSums(o.Total, o.Prepayment))
            .FirstOrDefaultAsync(ct);

    public async Task<SpentInOrder?> SpentInOrderAsync(int orderId, CancellationToken ct) =>
        await db.DdVoucherHistories.AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .GroupBy(h => h.VoucherId)
            .Select(g => new { VoucherId = g.Key, Spent = -g.Sum(h => h.Amount) })
            .Where(x => x.Spent != 0)
            .OrderBy(x => x.VoucherId)
            .Select(x => new SpentInOrder(x.VoucherId, x.Spent))
            .FirstOrDefaultAsync(ct);

    // Таблиці OpenCart — MyISAM, без транзакцій: ті самі записи, що й на оформленні в магазині, один за одним. Рядок сертифіката
    // стоїть одразу перед підсумком (sort_order 98 і 99), як їх сортує OpenCart.
    public async Task RedeemAsync(int orderId, int voucherId, string code, decimal amount, DateTime now, CancellationToken ct)
    {
        db.DdVoucherHistories.Add(new DdVoucherHistory { VoucherId = voucherId, OrderId = orderId, Amount = -amount, DateAdded = now });
        db.DdOrderTotals.Add(new DdOrderTotal
        {
            OrderId = orderId, Code = "voucher", Title = $"Подарунковий сертифікат ({code})", Value = -amount, SortOrder = 98,
        });
        await db.SaveChangesAsync(ct);
        await ChangeTotalAsync(orderId, -amount, ct);
    }

    public async Task ReleaseAsync(int orderId, int voucherId, decimal amount, CancellationToken ct)
    {
        await db.DdVoucherHistories.Where(h => h.OrderId == orderId && h.VoucherId == voucherId).ExecuteDeleteAsync(ct);
        await db.DdOrderTotals.Where(t => t.OrderId == orderId && t.Code == "voucher").ExecuteDeleteAsync(ct);
        await ChangeTotalAsync(orderId, amount, ct);
    }

    private async Task ChangeTotalAsync(int orderId, decimal by, CancellationToken ct)
    {
        await db.DdOrderTotals.Where(t => t.OrderId == orderId && t.Code == "total")
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Value, t => t.Value + by).SetProperty(t => t.SortOrder, 99), ct);
        await db.DdOrders.Where(o => o.OrderId == orderId).ExecuteUpdateAsync(s => s.SetProperty(o => o.Total, o => o.Total + by), ct);
    }

    public Task<bool> CodeTakenAsync(string code, int? exceptId, CancellationToken ct) =>
        db.DdVouchers.AnyAsync(v => v.Code == code && v.VoucherId != (exceptId ?? 0), ct);

    public async Task<int> AddAsync(GiftVoucher voucher, DateTime now, CancellationToken ct)
    {
        var row = new DdVoucher { OrderId = 0, DateAdded = now };
        Fill(row, voucher);
        db.DdVouchers.Add(row);
        await db.SaveChangesAsync(ct);
        return row.VoucherId;
    }

    public async Task UpdateAsync(int id, GiftVoucher voucher, CancellationToken ct)
    {
        var row = await db.DdVouchers.SingleAsync(v => v.VoucherId == id, ct);
        Fill(row, voucher);
        await db.SaveChangesAsync(ct);
    }

    public Task DeleteAsync(int id, CancellationToken ct) => db.DdVouchers.Where(v => v.VoucherId == id).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<VoucherHistoryRow>> HistoryAsync(int id, CancellationToken ct) =>
        await (
            from h in db.DdVoucherHistories.AsNoTracking()
            join o in db.DdOrders on h.OrderId equals o.OrderId into orders
            from o in orders.DefaultIfEmpty()
            where h.VoucherId == id
            orderby h.VoucherHistoryId
            select new VoucherHistoryRow(h.OrderId, o == null ? "" : o.Firstname + " " + o.Lastname, h.Amount, h.DateAdded))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LookupItem>> ThemesAsync(CancellationToken ct) =>
        (await (
            from t in db.DdVoucherThemes.AsNoTracking()
            join d in db.DdVoucherThemeDescriptions.Where(d => d.LanguageId == _languageId) on t.VoucherThemeId equals d.VoucherThemeId into names
            from d in names.DefaultIfEmpty()
            select new { t.VoucherThemeId, Name = d == null ? null : d.Name })
            .ToListAsync(ct))
        .Select(t => new LookupItem(t.VoucherThemeId, t.Name ?? $"#{t.VoucherThemeId}"))
        .OrderBy(t => t.Name, StringComparer.CurrentCulture)
        .ToList();

    // Витрачена сума й статус замовлення, в якому сертифікат куплено, — одним рядком на сертифікат.
    private IQueryable<VoucherRow> Rows(IQueryable<DdVoucher> vouchers) =>
        from v in vouchers.AsNoTracking()
        select new VoucherRow(v.VoucherId, v.OrderId, v.Code, v.FromName, v.FromEmail, v.ToName, v.ToEmail, v.VoucherThemeId,
            db.DdVoucherThemeDescriptions.Where(d => d.VoucherThemeId == v.VoucherThemeId && d.LanguageId == _languageId).Select(d => d.Name)
                .FirstOrDefault() ?? "",
            v.Message, v.Amount, v.Status, v.DateAdded,
            db.DdVoucherHistories.Where(h => h.VoucherId == v.VoucherId).Sum(h => (decimal?)h.Amount) ?? 0,
            v.OrderId > 0 ? db.DdOrders.Where(o => o.OrderId == v.OrderId).Select(o => (int?)o.OrderStatusId).FirstOrDefault() : null);

    private static void Fill(DdVoucher row, GiftVoucher v)
    {
        row.Code = v.Code;
        row.FromName = v.FromName;
        row.FromEmail = v.FromEmail;
        row.ToName = v.ToName;
        row.ToEmail = v.ToEmail;
        row.VoucherThemeId = v.ThemeId;
        row.Message = v.Message;
        row.Amount = v.Amount;
        row.Status = v.Active;
    }
}
