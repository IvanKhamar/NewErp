using NewErp.Application.Common;
using NewErp.Domain.Customers;
using NewErp.Domain.Orders;

namespace NewErp.Application.Customers;

/// <summary>Покупці (dd_customer), як «Покупці → Покупці» старого сайту, з історією замовлень і нотатками менеджерів.</summary>
public sealed class CustomerService(ICustomerStore store, TimeProvider clock)
{
    public Task<PagedResult<CustomerListItem>> ListAsync(CustomerFilter filter, CancellationToken ct)
    {
        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize);
        return store.ListAsync(filter with { Page = page, PageSize = pageSize, Search = filter.Search?.Trim() }, ct);
    }

    public Task<IReadOnlyList<CustomerGroupDto>> GroupsAsync(CancellationToken ct) => store.GroupsAsync(ct);

    public async Task<CustomerDetails> GetAsync(int customerId, CancellationToken ct)
    {
        var c = await store.GetAsync(customerId, ct) ?? throw new NotFoundException($"Покупця {customerId} не знайдено.");
        var orders = await store.OrdersAsync(customerId, CustomerRisk.PhoneKey(c.Telephone), ct);
        var summary = CustomerSummary.From(orders.Select(o => new CustomerOrder(o.Id, o.DateAdded, o.StatusId, o.Total)).ToList());
        var risk = CustomerRisk.From(orders.Select(o => (o.Id, o.StatusId)).ToList());
        return new CustomerDetails(
            c.Id, c.Firstname, c.Lastname, c.Email, c.Telephone, c.GroupName, c.Status, c.Newsletter, c.DateAdded, c.Addresses, orders,
            new CustomerStatsDto(summary.OrderCount, summary.CancelledCount, summary.Spent, summary.FirstOrder, summary.LastOrder,
                risk.Level.ToString(), risk.Message),
            c.Notes);
    }

    public async Task AddNoteAsync(int customerId, AddCustomerNoteRequest request, CancellationToken ct)
    {
        var comment = CustomerNote.Validate(request.Comment);
        if (!await store.AddNoteAsync(customerId, comment, clock.GetLocalNow().DateTime, ct))
            throw new NotFoundException($"Покупця {customerId} не знайдено.");
    }
}
