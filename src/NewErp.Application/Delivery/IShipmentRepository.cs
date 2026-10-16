using NewErp.Domain.Delivery;

namespace NewErp.Application.Delivery;

public interface IShipmentRepository
{
    Task<ShipmentDto?> GetForOrderAsync(int orderId, CancellationToken ct);

    /// <summary>ТТН із номером, які ще можуть змінитися (не отримано, не повернено), для зміни статусу.</summary>
    Task<IReadOnlyList<OpenShipment>> ListOpenForTrackingAsync(CancellationToken ct);

    /// <summary>Посилки на відділенні, у яких платне зберігання починається в цьому проміжку, і клієнту ще не нагадували.</summary>
    Task<IReadOnlyList<ReminderDto>> ListRemindersAsync(DateOnly keepingFrom, DateOnly keepingTo, CancellationToken ct);

    /// <returns>false, якщо замовлення немає.</returns>
    Task<bool> MarkRemindedAsync(int orderId, DateTime remindedAt, CancellationToken ct);

    /// <summary>Остання ТТН замовлення для зміни (друк, видалення) разом з відправником.</summary>
    Task<StoredShipment?> GetStoredAsync(int orderId, CancellationToken ct);

    /// <returns>null, якщо замовлення немає.</returns>
    Task<OrderShippingInfo?> GetOrderShippingInfoAsync(int orderId, CancellationToken ct);

    /// <summary>Нова ТТН і відправник, від якого її створено (збережеться разом із SaveChangesAsync).</summary>
    void Add(ShipmentDocument document, string senderId, int userId, DateTime createdAt);

    /// <summary>Видаляє ТТН і запис про відправника одразу, без SaveChangesAsync.</summary>
    Task DeleteAsync(int documentId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
