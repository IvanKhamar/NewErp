using NewErp.Application.Common;

namespace NewErp.Application.Orders;

/// <summary>Читання замовлень. Реалізація в Infrastructure будує LINQ-запити до бази.</summary>
public interface IOrderQueries
{
    Task<PagedResult<OrderListItem>> ListAsync(OrderFilter filter, CancellationToken ct);

    Task<OrderDetails?> GetAsync(int orderId, CancellationToken ct);

    Task<IReadOnlyList<LookupItem>> ListStatusesAsync(CancellationToken ct);

    Task<IReadOnlyList<LookupItem>> ListSourcesAsync(CancellationToken ct);
}