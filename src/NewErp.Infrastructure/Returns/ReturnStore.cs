using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Common;
using NewErp.Application.Returns;
using NewErp.Domain.Returns;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Returns;

public sealed class ReturnStore(ErpDbContext db, IOptions<ErpOptions> options) : IReturnStore
{
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<PagedResult<ReturnListItem>> ListAsync(ReturnFilter filter, CancellationToken ct)
    {
        var query = db.DdReturns.AsNoTracking();
        if (filter.ReturnId is { } returnId)
            query = query.Where(r => r.ReturnId == returnId);
        if (filter.OrderId is { } orderId)
            query = query.Where(r => r.OrderId == orderId);
        if (filter.StatusId is { } statusId)
            query = query.Where(r => r.ReturnStatusId == statusId);
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var term = filter.Search;
            query = query.Where(r => (r.Firstname + " " + r.Lastname).Contains(term) || r.Telephone.Contains(term)
                || r.Product.Contains(term) || r.Model.Contains(term));
        }

        var total = await query.CountAsync(ct);
        // date_ordered не читаємо: у справжній базі там може бути '0000-00-00', який DateTime не прочитає.
        var rows = await query
            .OrderByDescending(r => r.ReturnId)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(r => new
            {
                r.ReturnId, r.OrderId, r.Firstname, r.Lastname, r.Telephone, r.Product, r.Model, r.Quantity,
                r.ReturnStatusId, r.ReturnActionId, r.DateAdded, r.DateModified,
            })
            .ToListAsync(ct);

        var lookups = await LookupsAsync(ct);
        var statuses = lookups.Statuses.ToDictionary(s => s.Id, s => s.Name);
        var actions = lookups.Actions.ToDictionary(a => a.Id, a => a.Name);
        var items = rows.Select(r => new ReturnListItem(
            r.ReturnId, r.OrderId, $"{r.Firstname} {r.Lastname}".Trim(), r.Telephone, r.Product, r.Model, r.Quantity,
            r.ReturnStatusId, statuses.GetValueOrDefault(r.ReturnStatusId), actions.GetValueOrDefault(r.ReturnActionId),
            r.DateAdded, r.DateModified)).ToList();
        return new PagedResult<ReturnListItem>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<ReturnDetails?> GetAsync(int returnId, CancellationToken ct)
    {
        var r = await db.DdReturns.AsNoTracking()
            .Where(x => x.ReturnId == returnId)
            .Select(x => new
            {
                x.ReturnId, x.OrderId, x.ProductId, x.Firstname, x.Lastname, x.Telephone, x.Email, x.Product, x.Model, x.Quantity,
                x.Opened, x.ReturnReasonId, x.ReturnActionId, x.ReturnStatusId, x.Comment, x.DateAdded, x.DateModified,
            })
            .FirstOrDefaultAsync(ct);
        if (r is null)
            return null;

        var lookups = await LookupsAsync(ct);
        string? Name(IReadOnlyList<ReturnLookupItem> items, int id) => items.FirstOrDefault(i => i.Id == id)?.Name;
        var history = await db.DdReturnHistories.AsNoTracking()
            .Where(h => h.ReturnId == returnId)
            .OrderBy(h => h.DateAdded).ThenBy(h => h.ReturnHistoryId)
            .Select(h => new { h.DateAdded, h.ReturnStatusId, h.Comment })
            .ToListAsync(ct);

        return new ReturnDetails(
            r.ReturnId, r.OrderId, r.ProductId, $"{r.Firstname} {r.Lastname}".Trim(), r.Telephone, r.Email, r.Product, r.Model, r.Quantity,
            r.Opened, r.ReturnReasonId, Name(lookups.Reasons, r.ReturnReasonId), r.ReturnActionId, Name(lookups.Actions, r.ReturnActionId),
            r.ReturnStatusId, Name(lookups.Statuses, r.ReturnStatusId), r.Comment ?? "", r.DateAdded, r.DateModified,
            history.Select(h => new ReturnHistoryEntry(h.DateAdded, h.ReturnStatusId, Name(lookups.Statuses, h.ReturnStatusId), h.Comment)).ToList());
    }

    public async Task<ReturnLookupsDto> LookupsAsync(CancellationToken ct)
    {
        var statuses = await db.DdReturnStatuses.AsNoTracking().Where(s => s.LanguageId == _languageId)
            .OrderBy(s => s.ReturnStatusId).Select(s => new ReturnLookupItem(s.ReturnStatusId, s.Name)).ToListAsync(ct);
        var reasons = await db.DdReturnReasons.AsNoTracking().Where(s => s.LanguageId == _languageId)
            .OrderBy(s => s.ReturnReasonId).Select(s => new ReturnLookupItem(s.ReturnReasonId, s.Name)).ToListAsync(ct);
        var actions = await db.DdReturnActions.AsNoTracking().Where(s => s.LanguageId == _languageId)
            .OrderBy(s => s.ReturnActionId).Select(s => new ReturnLookupItem(s.ReturnActionId, s.Name)).ToListAsync(ct);
        return new ReturnLookupsDto(statuses, reasons, actions);
    }

    public async Task<ReturnSourceOrder?> GetSourceOrderAsync(int orderId, CancellationToken ct)
    {
        var order = await db.DdOrders.AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => new { o.Firstname, o.Lastname, o.Telephone, o.Email, o.CustomerId, o.DateAdded })
            .FirstOrDefaultAsync(ct);
        if (order is null)
            return null;

        var lines = await db.DdOrderProducts.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.OrderProductId)
            .Select(p => new ReturnableLine(p.ProductId, p.Name, p.Model, p.Quantity))
            .ToListAsync(ct);
        var returned = await db.DdReturns.AsNoTracking()
            .Where(r => r.OrderId == orderId)
            .GroupBy(r => r.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(r => r.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, ct);

        return new ReturnSourceOrder(
            orderId, order.Firstname, order.Lastname, order.Telephone, order.Email, order.CustomerId, order.DateAdded, lines, returned);
    }

    public async Task<int> CreateAsync(NewReturnRecord record, CancellationToken ct)
    {
        var (order, line, request) = (record.Order, record.Line, record.Request);
        var comment = (request.Comment ?? "").Trim();
        var row = new DdReturn
        {
            OrderId = order.OrderId,
            ProductId = line.ProductId,
            CustomerId = order.CustomerId,
            Firstname = order.Firstname.Trim(),
            Lastname = order.Lastname.Trim(),
            Email = order.Email.Trim(),
            Telephone = order.Telephone.Trim(),
            Product = line.Name,
            Model = line.Model,
            Quantity = request.Quantity,
            Opened = request.Opened,
            ReturnReasonId = request.ReasonId,
            ReturnActionId = request.ActionId,
            ReturnStatusId = request.StatusId,
            Comment = comment,
            DateOrdered = order.DateAdded.Date,
            DateAdded = record.Now,
            DateModified = record.Now,
        };
        db.DdReturns.Add(row);
        await db.SaveChangesAsync(ct);

        // Як форма старого сайту: перший статус теж потрапляє в журнал.
        db.DdReturnHistories.Add(new DdReturnHistory
        {
            ReturnId = row.ReturnId, ReturnStatusId = request.StatusId, Notify = false, Comment = comment, DateAdded = record.Now,
        });
        await db.SaveChangesAsync(ct);
        return row.ReturnId;
    }

    public async Task<bool> AddHistoryAsync(int returnId, int statusId, string comment, int? actionId, DateTime now, CancellationToken ct)
    {
        // MyISAM: без транзакції, тому спершу статус (як addReturnHistory старого сайту), потім журнал.
        var updated = actionId is { } action
            ? await db.DdReturns.Where(r => r.ReturnId == returnId).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ReturnStatusId, statusId).SetProperty(r => r.ReturnActionId, action).SetProperty(r => r.DateModified, now), ct)
            : await db.DdReturns.Where(r => r.ReturnId == returnId).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ReturnStatusId, statusId).SetProperty(r => r.DateModified, now), ct);
        if (updated == 0)
            return false;

        db.DdReturnHistories.Add(new DdReturnHistory { ReturnId = returnId, ReturnStatusId = statusId, Notify = false, Comment = comment, DateAdded = now });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
