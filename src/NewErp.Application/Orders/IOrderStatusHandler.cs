using NewErp.Domain.Orders;

namespace NewErp.Application.Orders;

/// <summary>
/// Те, що має статися при зміні статусу замовлення в інших модулях (наприклад, списання розхідників).
/// OrderService викликає всіх зареєстрованих обробників до збереження нового статусу.
/// </summary>
public interface IOrderStatusHandler
{
    /// <returns>Попередження для користувача (порожньо, якщо все гаразд).</returns>
    Task<IReadOnlyList<string>> OnStatusChangingAsync(OrderStatusChange change, CancellationToken ct);
}
