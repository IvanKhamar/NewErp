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

    /// <summary>Планова дата відправки (dd_order.date_shipping); null прибирає її.</summary>
    Task SaveShippingDateAsync(int orderId, DateOnly? day, CancellationToken ct);

    /// <summary>Наступний номер префікса рахунку замовлення, один раз; замовлення, що вже має номер, його зберігає. Повертає обидва разом.</summary>
    Task<string> CreateInvoiceNoAsync(int orderId, CancellationToken ct);

    /// <summary>Чому замовлення не можна видаляти (ТТН, чек, оплата, повернення, подарунковий сертифікат).</summary>
    Task<IReadOnlyList<string>> DeleteBlockersAsync(int orderId, CancellationToken ct);

    /// <summary>Замовлення з рядками, опціями, підсумками, менеджером, бонусними балами й комісією. Журнал статусів лишається.</summary>
    Task DeleteAsync(int orderId, CancellationToken ct);
}