using Microsoft.EntityFrameworkCore;

using NewErp.Application.Cashbox;
using NewErp.Application.Common;
using NewErp.Domain.Cashbox;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Cashbox;

public sealed class CashboxRepository(ErpDbContext db) : ICashboxRepository
{
    public async Task<IReadOnlyList<Cashier>> ListCashiersAsync(CancellationToken ct) => await db.Cashiers.ToListAsync(ct);

    public Task<Cashier?> GetCashierAsync(int id, CancellationToken ct) => db.Cashiers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public void Add(Cashier cashier) => db.Cashiers.Add(cashier);

    public async Task<IReadOnlyList<CashboxWarehouse>> ListWarehousesAsync(CancellationToken ct) =>
        await db.CashboxWarehouses.OrderBy(w => w.Id).ToListAsync(ct);

    public Task<CashboxWarehouse?> GetWarehouseAsync(int id, CancellationToken ct) => db.CashboxWarehouses.FirstOrDefaultAsync(w => w.Id == id, ct);

    public void Add(CashboxWarehouse warehouse) => db.CashboxWarehouses.Add(warehouse);

    public async Task<IReadOnlyList<CashboxRequisites>> ListRequisitesAsync(CancellationToken ct) =>
        await db.CashboxRequisites.OrderBy(r => r.Id).ToListAsync(ct);

    public Task<CashboxRequisites?> GetRequisitesAsync(int id, CancellationToken ct) => db.CashboxRequisites.FirstOrDefaultAsync(r => r.Id == id, ct);

    public void Add(CashboxRequisites requisites) => db.CashboxRequisites.Add(requisites);

    public async Task<bool> WarehouseInUseAsync(int id, CancellationToken ct) =>
        await db.Cashiers.AnyAsync(c => c.WarehouseId == id, ct) || await db.Cheques.AnyAsync(c => c.WarehouseId == id, ct);

    public async Task<bool> RequisitesInUseAsync(int id, CancellationToken ct) =>
        await db.Cashiers.AnyAsync(c => c.RequisitesId == id, ct) || await db.Cheques.AnyAsync(c => c.RequisitesId == id, ct);

    public void Remove(CashboxDirectoryEntry entry) => db.Remove(entry);

    public async Task<IReadOnlyList<WorkShift>> ListOpenShiftsAsync(CancellationToken ct) =>
        await db.WorkShifts.Where(s => s.DateEnd == null).ToListAsync(ct);

    public Task<WorkShift?> GetShiftAsync(int id, CancellationToken ct) => db.WorkShifts.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<WorkShiftDto>> ListShiftsAsync(WorkShiftFilter filter, CancellationToken ct)
    {
        var shifts = db.WorkShifts.AsNoTracking();
        if (filter.CashierId is { } cashierId)
            shifts = shifts.Where(s => s.CashierId == cashierId);
        if (filter.From is { } from)
            shifts = shifts.Where(s => s.DateStart >= from.ToDateTime(TimeOnly.MinValue));
        if (filter.To is { } to)
            shifts = shifts.Where(s => s.DateStart < to.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var rows = await (
            from s in shifts
            join c in db.Cashiers on s.CashierId equals c.Id into cs
            from c in cs.DefaultIfEmpty()
            join u in db.DdUsers on s.UserId equals u.UserId into us
            from u in us.DefaultIfEmpty()
            orderby s.Id descending
            select new
            {
                Shift = s,
                Cashier = c == null ? "" : c.Name,
                OpenedBy = u == null ? null : u.Firstname + " " + u.Lastname,
                Cheques = db.Cheques.Count(ch => ch.WorkShiftId == s.Id && ch.CheckboxId != ""),
                Sum = db.Cheques.Where(ch => ch.WorkShiftId == s.Id && ch.CheckboxId != "").Sum(ch => (decimal?)ch.Sum) ?? 0,
            }
        ).Take(500).ToListAsync(ct);

        return rows.Select(r => new WorkShiftDto(
            r.Shift.Id, r.Shift.CashierId, r.Cashier, r.Shift.CheckboxShiftId, r.OpenedBy?.Trim(), r.Shift.DateStart, r.Shift.DateEnd,
            r.Cheques, r.Sum)).ToList();
    }

    public void Add(WorkShift shift) => db.WorkShifts.Add(shift);

    public void Add(ZReport report) => db.ZReports.Add(report);

    public async Task<IReadOnlyList<ZReportDto>> ListReportsAsync(int? cashierId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var reports = db.ZReports.AsNoTracking();
        if (cashierId is { } id)
            reports = reports.Where(r => r.CashierId == id);
        if (from is { } f)
            reports = reports.Where(r => r.DateAdded >= f.ToDateTime(TimeOnly.MinValue));
        if (to is { } t)
            reports = reports.Where(r => r.DateAdded < t.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var rows = await (
            from r in reports
            join c in db.Cashiers on r.CashierId equals c.Id into cs
            from c in cs.DefaultIfEmpty()
            join u in db.DdUsers on r.UserId equals u.UserId into us
            from u in us.DefaultIfEmpty()
            orderby r.Id descending
            select new { Report = r, Cashier = c == null ? "" : c.Name, ClosedBy = u == null ? null : u.Firstname + " " + u.Lastname }
        ).Take(500).ToListAsync(ct);

        return rows.Select(r => new ZReportDto(
            r.Report.Id, r.Report.CashierId, r.Cashier, r.Report.CheckboxReportId, r.ClosedBy?.Trim(), r.Report.DateAdded)).ToList();
    }

    public async Task<IReadOnlyList<Cheque>> ListUnreportedChequesAsync(int cashierId, int shiftId, DateTime since, CancellationToken ct) =>
        await db.Cheques
            .Where(c => c.CashierId == cashierId && c.ReportId == 0
                && (c.WorkShiftId == shiftId || (c.WorkShiftId == 0 && c.DateAdded >= since)))
            .ToListAsync(ct);

    public async Task AttachChequesToReportAsync(IReadOnlyList<int> chequeIds, int reportId, CancellationToken ct)
    {
        if (chequeIds.Count == 0)
            return;
        var ids = chequeIds.ToList();
        await db.Cheques.Where(c => ids.Contains(c.Id)).ExecuteUpdateAsync(s => s.SetProperty(c => c.ReportId, reportId), ct);
    }

    public Task<Cheque?> GetChequeAsync(int id, CancellationToken ct) => db.Cheques.FirstOrDefaultAsync(c => c.Id == id, ct);

    // Чек на замовлення один; якщо їх кілька (старий сайт), беремо останній.
    public Task<Cheque?> GetChequeForOrderAsync(int orderId, CancellationToken ct) =>
        db.Cheques.Where(c => c.OrderId == orderId).OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct);

    public async Task<ChequeDto?> GetChequeDtoAsync(int id, CancellationToken ct) =>
        (await Project(db.Cheques.AsNoTracking().Where(c => c.Id == id)).ToListAsync(ct)).Select(ToDto).FirstOrDefault();

    public async Task<PagedResult<ChequeDto>> ListChequesAsync(ChequeFilter filter, CancellationToken ct)
    {
        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize);
        var cheques = db.Cheques.AsNoTracking();
        if (filter.CashierId is { } cashierId)
            cheques = cheques.Where(c => c.CashierId == cashierId);
        if (filter.OrderId is { } orderId)
            cheques = cheques.Where(c => c.OrderId == orderId);
        if (filter.From is { } from)
            cheques = cheques.Where(c => c.DateAdded >= from.ToDateTime(TimeOnly.MinValue));
        if (filter.To is { } to)
            cheques = cheques.Where(c => c.DateAdded < to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (filter.Fiscalized is { } fiscalized)
            cheques = fiscalized ? cheques.Where(c => c.CheckboxId != "") : cheques.Where(c => c.CheckboxId == "");

        var total = await cheques.CountAsync(ct);
        var rows = await Project(cheques.OrderByDescending(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return new PagedResult<ChequeDto>(rows.Select(ToDto).ToList(), page, pageSize, total);
    }

    public void Add(Cheque cheque) => db.Cheques.Add(cheque);

    public Task DeleteChequeAsync(int id, CancellationToken ct) => db.Cheques.Where(c => c.Id == id).ExecuteDeleteAsync(ct);

    public async Task<OrderForCheque?> GetOrderForChequeAsync(int orderId, CancellationToken ct)
    {
        var order = await db.DdOrders.AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => new { o.OrderId, o.Total, o.Prepayment, o.PaymentCode, o.PaymentMethod, o.Email })
            .FirstOrDefaultAsync(ct);
        if (order is null)
            return null;

        // Назва з рядка замовлення (як її побачив покупець), а не поточна назва товару в каталозі, як брав старий сайт.
        var lines = await db.DdOrderProducts.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.OrderProductId)
            .Select(p => new Domain.Cashbox.ReceiptSourceLine(p.ProductId, p.Name, p.Price, p.Quantity))
            .ToListAsync(ct);
        return new OrderForCheque(order.OrderId, order.Total, order.Prepayment, order.PaymentCode, order.PaymentMethod, order.Email, lines);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private IQueryable<ChequeRow> Project(IQueryable<Cheque> cheques) =>
        from c in cheques
        join o in db.DdOrders on c.OrderId equals o.OrderId into os
        from o in os.DefaultIfEmpty()
        join k in db.Cashiers on c.CashierId equals k.Id into ks
        from k in ks.DefaultIfEmpty()
        join w in db.CashboxWarehouses on c.WarehouseId equals w.Id into ws
        from w in ws.DefaultIfEmpty()
        join r in db.CashboxRequisites on c.RequisitesId equals r.Id into rs
        from r in rs.DefaultIfEmpty()
        select new ChequeRow(
            c, o == null ? null : o.Firstname + " " + o.Lastname, k == null ? null : k.Name, w == null ? null : w.Name, r == null ? null : r.Name);

    private static ChequeDto ToDto(ChequeRow r) => new(
        r.Cheque.Id, r.Cheque.OrderId, r.Customer?.Trim(), r.Cheque.CashierId, r.Cashier, r.Warehouse, r.Requisites, r.Cheque.Sum,
        r.Cheque.CheckboxId, r.Cheque.IsFiscalized, null, r.Cheque.WorkShiftId, r.Cheque.ReportId, r.Cheque.DateAdded);

    private sealed record ChequeRow(Cheque Cheque, string? Customer, string? Cashier, string? Warehouse, string? Requisites);
}
