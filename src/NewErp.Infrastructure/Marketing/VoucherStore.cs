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
