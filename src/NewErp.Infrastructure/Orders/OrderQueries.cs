using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Common;
using NewErp.Application.Orders;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Orders;

public sealed class OrderQueries(ErpDbContext db, IOptions<ErpOptions> options) : IOrderQueries
{
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<PagedResult<OrderListItem>> ListAsync(OrderFilter filter, CancellationToken ct)
    {
        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize);

        // OpenCart зберігає замовлення з order_status_id = 0 як "незавершені" (кошик не оформлено), їх не показуємо.
        var orders = db.DdOrders.AsNoTracking().Where(o => o.OrderStatusId > 0);

        if (filter.StatusId is { } statusId)
            orders = orders.Where(o => o.OrderStatusId == statusId);
        if (filter.SourceId is { } sourceId)
            orders = orders.Where(o => o.OrderSourceId == sourceId);
        if (filter.ManagerId is { } managerId)
            orders = orders.Where(o => db.ErpOrderManagers.Any(m => m.OrderId == o.OrderId && m.UserId == managerId));
        if (filter.From is { } from)
            orders = orders.Where(o => o.DateAdded >= from.ToDateTime(TimeOnly.MinValue));
        if (filter.To is { } to)
            orders = orders.Where(o => o.DateAdded < to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            orders = int.TryParse(s, out var id)
                ? orders.Where(o => o.OrderId == id || o.Telephone.Contains(s))
                : orders.Where(o => o.Telephone.Contains(s) || o.Lastname.Contains(s) || o.Firstname.Contains(s) || o.Email.Contains(s));
        }

        var total = await orders.CountAsync(ct);

        // Ліві з'єднання (left join) з довідниками: замовлення показується, навіть якщо статусу чи менеджера немає.
        var items = await (
            from o in orders
            join st in db.DdOrderStatuses.Where(x => x.LanguageId == _languageId) on o.OrderStatusId equals st.OrderStatusId into sts
            from st in sts.DefaultIfEmpty()
            join src in db.DdOrderSources.Where(x => x.LanguageId == _languageId) on o.OrderSourceId equals src.OrderSourceId into srcs
            from src in srcs.DefaultIfEmpty()
            join m in db.ErpOrderManagers on o.OrderId equals m.OrderId into ms
            from m in ms.DefaultIfEmpty()
            join u in db.DdUsers on m.UserId equals u.UserId into us
            from u in us.DefaultIfEmpty()
            orderby o.DateAdded descending, o.OrderId descending
            select new OrderListItem(
                o.OrderId,
                o.DateAdded,
                o.Firstname + " " + o.Lastname,
                o.Telephone,
                o.Total,
                o.CurrencyCode,
                o.OrderStatusId,
                st.Name,
                src.Name,
                m == null ? null : m.UserId,
                u == null ? null : u.Firstname + " " + u.Lastname))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderListItem>(items, page, pageSize, total);
    }

    public async Task<OrderDetails?> GetAsync(int orderId, CancellationToken ct)
    {
        var o = await db.DdOrders.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (o is null)
            return null;

        var statusNames = await db.DdOrderStatuses.AsNoTracking()
            .Where(s => s.LanguageId == _languageId)
            .ToDictionaryAsync(s => s.OrderStatusId, s => s.Name, ct);

        var sourceName = await db.DdOrderSources.AsNoTracking()
            .Where(s => s.OrderSourceId == o.OrderSourceId && s.LanguageId == _languageId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);

        var manager = await (
            from m in db.ErpOrderManagers
            join u in db.DdUsers on m.UserId equals u.UserId
            where m.OrderId == orderId
            select new { m.UserId, Name = u.Firstname + " " + u.Lastname })
            .FirstOrDefaultAsync(ct);

        var products = await db.DdOrderProducts.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.OrderProductId)
            .Select(p => new OrderProductLine(p.ProductId, p.Name, p.Model, p.Quantity, p.Price, p.Total))
            .ToListAsync(ct);

        var history = await (
            from l in db.DdOrderStatusLogs
            join u in db.DdUsers on l.UserId equals u.UserId into us
            from u in us.DefaultIfEmpty()
            where l.OrderId == orderId
            orderby l.DateAdded, l.OrderStatusLogId
            select new { l.DateAdded, l.OldStatusId, l.NewStatusId, l.UserId, UserName = u == null ? null : u.Firstname + " " + u.Lastname })
            .ToListAsync(ct);

        return new OrderDetails(
            o.OrderId,
            o.DateAdded,
            $"{o.Firstname} {o.Lastname}",
            o.Telephone,
            o.Email,
            o.ShippingMethod,
            o.ShippingCity,
            $"{o.ShippingAddress1} {o.ShippingAddress2}".Trim(),
            o.PaymentMethod,
            o.Comment,
            o.Total,
            o.CurrencyCode,
            o.OrderStatusId,
            statusNames.GetValueOrDefault(o.OrderStatusId),
            o.OrderSourceId,
            sourceName,
            manager?.UserId,
            manager?.Name,
            products,
            history.Select(h => new OrderStatusHistoryEntry(h.DateAdded, h.OldStatusId, h.NewStatusId, statusNames.GetValueOrDefault(h.NewStatusId), h.UserId, h.UserName)).ToList());
    }

    public async Task<IReadOnlyList<LookupItem>> ListStatusesAsync(CancellationToken ct) =>
        await db.DdOrderStatuses.AsNoTracking()
            .Where(s => s.LanguageId == _languageId)
            .OrderBy(s => s.OrderStatusId)
            .Select(s => new LookupItem(s.OrderStatusId, s.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LookupItem>> ListSourcesAsync(CancellationToken ct) =>
        await db.DdOrderSources.AsNoTracking()
            .Where(s => s.LanguageId == _languageId)
            .OrderBy(s => s.OrderSourceId)
            .Select(s => new LookupItem(s.OrderSourceId, s.Name))
            .ToListAsync(ct);
}