using NewErp.Domain.Orders;

namespace NewErp.Application.Orders;

/// <summary>Те, що потрібно сценаріям зміни замовлення від бази даних.</summary>
public interface IOrderStore
{
    /// <returns>Поточний статус або null, якщо замовлення немає.</returns>
    Task<int?> GetStatusIdAsync(int orderId, CancellationToken ct);

    Task<bool> StatusExistsAsync(int statusId, CancellationToken ct);

    Task<bool> IsActiveUserAsync(int userId, CancellationToken ct);

    /// <summary>Оновлює статус у замовленні й пише запис у журнал змін.</summary>
    Task SaveStatusChangeAsync(OrderStatusChange change, CancellationToken ct);

    Task SaveManagerAsync(int orderId, int managerId, int assignedByUserId, DateTime assignedAt, CancellationToken ct);
}