using NewErp.Domain.Delivery;

namespace NewErp.Application.Delivery;

/// <summary>ТТН замовлення. Tone — як розфарбувати статус (Info, Success, Warning, Error).</summary>
public sealed record ShipmentDto(
    int Id,
    int OrderId,
    string Carrier,
    string Number,
    decimal Cost,
    string Status,
    int StatusCode,
    ShipmentTone Tone,
    bool IsFinal,
    string RecipientAddress,
    bool Redelivery,
    DateTime? DateCreated,
    DateOnly? DateScheduled,
    DateOnly? DatePayedKeeping);

/// <summary>
/// Результат оновлення статусів. ClientConfigured = false — ключ API Нової Пошти не задано,
/// тож нічого не перевірялось (локально так і має бути).
/// </summary>
public sealed record TrackingRefreshResult(bool ClientConfigured, int Checked, int Updated);

/// <summary>Посилка чекає на відділенні, скоро зберігання стане платним — клієнту варто нагадати.</summary>
public sealed record ReminderDto(
    int OrderId, string Customer, string Telephone, string Number, string Status, DateOnly DatePayedKeeping, string? ManagerName);

/// <summary>Що треба трекінгу: номер ТТН і телефон отримувача (з ним API повертає повні дані).</summary>
public sealed record TrackingRequest(string Number, string Phone);

/// <summary>Незакрита ТТН для трекінгу разом із телефоном замовлення.</summary>
public sealed record OpenShipment(ShipmentDocument Document, string Phone);
