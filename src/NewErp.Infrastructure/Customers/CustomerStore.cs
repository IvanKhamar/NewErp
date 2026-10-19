using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Common;
using NewErp.Application.Customers;
using NewErp.Domain.Orders;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Customers;

/// <summary>Пароль, сіль, токен і кошик покупця ніколи не читаємо: усі запити вибирають лише потрібні колонки.</summary>
public sealed class CustomerStore(ErpDbContext db, IOptions<ErpOptions> options) : ICustomerStore
{
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<PagedResult<CustomerListItem>> ListAsync(CustomerFilter filter, CancellationToken ct)
    {
        var query = db.DdCustomers.AsNoTracking();
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var term = filter.Search;
            query = int.TryParse(term, out var id)
                ? query.Where(c => c.CustomerId == id || c.Telephone.Contains(term))
                : query.Where(c => (c.Firstname + " " + c.Lastname).Contains(term) || c.Email.Contains(term) || c.Telephone.Contains(term));
        }
        if (filter.GroupId is { } groupId)
            query = query.Where(c => c.CustomerGroupId == groupId);
        if (filter.Status is { } status)
            query = query.Where(c => c.Status == status);

        var total = await query.CountAsync(ct);
        var items = await (
            from c in query
            join g in db.DdCustomerGroupDescriptions.Where(x => x.LanguageId == _languageId) on c.CustomerGroupId equals g.CustomerGroupId into gs
            from g in gs.DefaultIfEmpty()
            orderby c.DateAdded descending, c.CustomerId descending
            select new CustomerListItem(
                c.CustomerId, c.Firstname + " " + c.Lastname, c.Email, c.Telephone, g == null ? null : g.Name, c.Status, c.DateAdded,
                db.DdOrders.Count(o => o.CustomerId == c.CustomerId && o.OrderStatusId > 0)))
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
        return new PagedResult<CustomerListItem>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<IReadOnlyList<CustomerGroupDto>> GroupsAsync(CancellationToken ct) =>
        await db.DdCustomerGroupDescriptions.AsNoTracking()
            .Where(g => g.LanguageId == _languageId)
            .OrderBy(g => g.Name)
            .Select(g => new CustomerGroupDto(g.CustomerGroupId, g.Name))
            .ToListAsync(ct);

    public async Task<CustomerProfile?> GetAsync(int customerId, CancellationToken ct)
    {
        var c = await db.DdCustomers.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .Select(x => new
            {
                x.CustomerId, x.Firstname, x.Lastname, x.Email, x.Telephone, x.CustomerGroupId, x.Status, x.Newsletter, x.AddressId, x.DateAdded,
            })
            .FirstOrDefaultAsync(ct);
        if (c is null)
            return null;

        var group = await db.DdCustomerGroupDescriptions.AsNoTracking()
            .Where(g => g.CustomerGroupId == c.CustomerGroupId && g.LanguageId == _languageId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(ct);
        var addresses = await db.DdAddresses.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderBy(a => a.AddressId)
            .Select(a => new CustomerAddressDto(
                a.AddressId, a.Firstname + " " + a.Lastname, a.City, a.Address1, a.Address2, a.AddressId == c.AddressId))
            .ToListAsync(ct);
        var notes = await db.DdCustomerHistories.AsNoTracking()
            .Where(h => h.CustomerId == customerId)
            .OrderByDescending(h => h.DateAdded).ThenByDescending(h => h.CustomerHistoryId)
            .Select(h => new CustomerNoteDto(h.DateAdded, h.Comment))
            .ToListAsync(ct);

        return new CustomerProfile(
            c.CustomerId, c.Firstname, c.Lastname, c.Email, c.Telephone, group, c.Status, c.Newsletter, c.DateAdded, addresses, notes);
    }

    public async Task<IReadOnlyList<CustomerOrderItem>> OrdersAsync(int customerId, string? phoneKey, CancellationToken ct)
    {
        // Телефон у замовленнях записаний як завгодно, тож у базі відбираємо за цифрами, а точно порівнюємо вже тут.
        var rows = await (
            from o in db.DdOrders.AsNoTracking()
            where o.OrderStatusId > 0 && (o.CustomerId == customerId || (phoneKey != null
                && o.Telephone.Replace("+", "").Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "").EndsWith(phoneKey)))
            join st in db.DdOrderStatuses.Where(x => x.LanguageId == _languageId) on o.OrderStatusId equals st.OrderStatusId into sts
            from st in sts.DefaultIfEmpty()
            orderby o.DateAdded descending, o.OrderId descending
            select new { o.OrderId, o.DateAdded, o.OrderStatusId, StatusName = st == null ? null : st.Name, o.Total, o.CurrencyCode, o.CustomerId, o.Telephone })
            .ToListAsync(ct);

        return rows
            .Where(o => o.CustomerId == customerId || CustomerRisk.PhoneKey(o.Telephone) == phoneKey)
            .Select(o => new CustomerOrderItem(o.OrderId, o.DateAdded, o.OrderStatusId, o.StatusName, o.Total, o.CurrencyCode, o.CustomerId != customerId))
            .ToList();
    }

    public async Task<bool> AddNoteAsync(int customerId, string comment, DateTime now, CancellationToken ct)
    {
        if (!await db.DdCustomers.AnyAsync(c => c.CustomerId == customerId, ct))
            return false;
        db.DdCustomerHistories.Add(new DdCustomerHistory { CustomerId = customerId, Comment = comment, DateAdded = now });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
