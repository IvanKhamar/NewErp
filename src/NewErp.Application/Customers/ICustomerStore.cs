using NewErp.Application.Common;

namespace NewErp.Application.Customers;

public interface ICustomerStore
{
    Task<PagedResult<CustomerListItem>> ListAsync(CustomerFilter filter, CancellationToken ct);

    Task<IReadOnlyList<CustomerGroupDto>> GroupsAsync(CancellationToken ct);

    Task<CustomerProfile?> GetAsync(int customerId, CancellationToken ct);

    /// <summary>Замовлення покупця: за customer_id і за тим самим телефоном (phoneKey — останні 9 цифр), бо гості його не мають.</summary>
    Task<IReadOnlyList<CustomerOrderItem>> OrdersAsync(int customerId, string? phoneKey, CancellationToken ct);

    /// <summary>false, якщо такого покупця немає.</summary>
    Task<bool> AddNoteAsync(int customerId, string comment, DateTime now, CancellationToken ct);
}
