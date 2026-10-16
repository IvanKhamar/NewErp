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

    Task SaveChangesAsync(CancellationToken ct);
}
