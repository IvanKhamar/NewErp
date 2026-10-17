using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Common;
using NewErp.Application.Payments;
using NewErp.Domain.Payments;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Payments;

public sealed class BankPaymentRepository(ErpDbContext db, IOptions<ErpOptions> options) : IBankPaymentRepository
{
    public async Task<PagedResult<BankPaymentDto>> ListAsync(BankPaymentFilter filter, CancellationToken ct)
    {
        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize);
        var payments = db.BankPayments.AsNoTracking().Where(p => p.IsVisible == !filter.Hidden);
        if (filter.From is { } from)
            payments = payments.Where(p => p.TranDate >= from);
        if (filter.To is { } to)
            payments = payments.Where(p => p.TranDate <= to);
        if (filter.Linked is { } linked)
            payments = linked ? payments.Where(p => p.OrderId != 0) : payments.Where(p => p.OrderId == 0);
        if (filter.OrderId is { } orderId)
            payments = payments.Where(p => p.OrderId == orderId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            payments = payments.Where(p => p.Description.Contains(search) || p.Terminal.Contains(search) || p.AmountText.StartsWith(search));
        }

        var total = await payments.CountAsync(ct);
        var rows = await Project(payments.OrderByDescending(p => p.TranDate).ThenByDescending(p => p.TranTime).ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return new PagedResult<BankPaymentDto>(rows.Select(ToDto).ToList(), page, pageSize, total);
    }

    public Task<BankPayment?> GetAsync(int id, CancellationToken ct) => db.BankPayments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<BankPaymentDto?> GetDtoAsync(int id, CancellationToken ct) =>
        (await Project(db.BankPayments.AsNoTracking().Where(p => p.Id == id)).ToListAsync(ct)).Select(ToDto).FirstOrDefault();

    public async Task<IReadOnlyList<BankPaymentDto>> ListForOrderAsync(int orderId, CancellationToken ct) =>
        (await Project(db.BankPayments.AsNoTracking().Where(p => p.OrderId == orderId).OrderBy(p => p.TranDate).ThenBy(p => p.TranTime))
            .ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlySet<string>> ExistingAppCodesAsync(IReadOnlyCollection<string> appCodes, CancellationToken ct)
    {
        if (appCodes.Count == 0)
            return new HashSet<string>();
        var codes = appCodes.ToList();
        return (await db.BankPayments.Where(p => codes.Contains(p.AppCode)).Select(p => p.AppCode).ToListAsync(ct)).ToHashSet();
    }

    public async Task<IReadOnlyList<OrderCandidate>> FindCandidatesAsync(CandidateQuery query, CancellationToken ct)
    {
        var amounts = query.Amounts.ToList();
        var ids = query.OrderIds.ToList();
        var customer = query.Customer;
        var customerOrderId = int.TryParse(customer, out var number) ? number : -1;
        var languageId = options.Value.LanguageId;

        // Незавершені замовлення (статус 0 — кошик, який покупець не оформив) не пропонуємо.
        var orders = db.DdOrders.AsNoTracking().Where(o => o.OrderStatusId != 0 && (
            ids.Contains(o.OrderId)
            || (customer != null && (o.Firstname.Contains(customer) || o.Lastname.Contains(customer) || o.Telephone.Contains(customer)
                || o.OrderId == customerOrderId))
            || (o.DateAdded >= query.From && o.DateAdded < query.To
                && (amounts.Contains(o.Total) || amounts.Contains(o.Prepayment) || amounts.Contains(o.Total - o.Prepayment)))));

        var rows = await (
            from o in orders
            join s in db.DdOrderStatuses.Where(s => s.LanguageId == languageId) on o.OrderStatusId equals s.OrderStatusId into ss
            from s in ss.DefaultIfEmpty()
            orderby o.OrderId descending
            select new
            {
                o.OrderId, o.Firstname, o.Lastname, o.Telephone, o.Total, o.Prepayment, o.DateAdded,
                Status = s == null ? "" : s.Name,
                LinkedPaymentId = db.BankPayments.Where(p => p.OrderId == o.OrderId).Select(p => (int?)p.Id).FirstOrDefault(),
            }
        ).Take(query.Limit).ToListAsync(ct);

        return rows.Select(r => new OrderCandidate(
            r.OrderId, $"{r.Firstname} {r.Lastname}".Trim(), r.Telephone, r.Total, r.Prepayment, r.DateAdded, r.Status, r.LinkedPaymentId)).ToList();
    }

    public Task<bool> OrderExistsAsync(int orderId, CancellationToken ct) => db.DdOrders.AnyAsync(o => o.OrderId == orderId, ct);

    public void Add(BankPayment payment) => db.BankPayments.Add(payment);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private IQueryable<PaymentRow> Project(IQueryable<BankPayment> payments) =>
        from p in payments
        join o in db.DdOrders on p.OrderId equals o.OrderId into os
        from o in os.DefaultIfEmpty()
        select new PaymentRow(p, o == null ? null : o.Firstname + " " + o.Lastname);

    private static BankPaymentDto ToDto(PaymentRow r) => new(
        r.Payment.Id, r.Payment.AppCode, r.Payment.Card, r.Payment.Amount, BankPayment.ParseMoney(r.Payment.RestText), r.Payment.Terminal,
        r.Payment.Description, r.Payment.Timestamp, r.Payment.OrderId, r.Customer?.Trim(), r.Payment.IsVisible);

    private sealed record PaymentRow(BankPayment Payment, string? Customer);
}
