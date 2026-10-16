using NewErp.Application.Common;

namespace NewErp.Application.Orders;

/// <summary>Читання замовлень. Реалізація в Infrastructure будує LINQ-запити до бази.</summary>
public interface IOrderQueries
{
    Task<PagedResult<OrderListItem>> ListAsync(OrderFilter filter, CancellationToken ct);

    Task<OrderDetails?> GetAsync(int orderId, CancellationToken ct);

    Task<IReadOnlyList<OrderStatusDto>> ListStatusesAsync(CancellationToken ct);

    Task<IReadOnlyList<LookupItem>> ListSourcesAsync(CancellationToken ct);

    /// <summary>Кого можна призначити відповідальним: активні користувачі старого сайту (як і там, будь-хто з dd_user).</summary>
    Task<IReadOnlyList<LookupItem>> ListManagersAsync(CancellationToken ct);
}