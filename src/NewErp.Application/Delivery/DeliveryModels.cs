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

/// <summary>Відправник з налаштувань (NovaPoshta:Senders). Ключ API сюди не потрапляє — він лишається на сервері.</summary>
public sealed record SenderOption(string Id, string Name, bool IsDefault);

/// <summary>Звідки відправляємо (NovaPoshta:DropoffPoints): відділення, куди везуть посилки.</summary>
public sealed record DropoffPointOption(string Id, string Name, bool IsDefault);

public sealed record CityDto(string Ref, string Name);

public sealed record WarehouseDto(string Ref, string Name, string Category, int MaxWeight);

/// <summary>
/// Заповнена наперед форма ТТН: отримувач, місто й відділення із замовлення (Ref — якщо знайшлися в довіднику),
/// оголошена вартість = сума замовлення, накладений платіж — сума замовлення, якщо оплата при отриманні.
/// CanCreate = false — ключа API немає і тестові ТТН вимкнено; TestMode — ключа немає, створюються лише тестові ТТН.
/// </summary>
public sealed record ShipmentFormDto(
    string FirstName,
    string LastName,
    string Phone,
    string CityName,
    CityDto? City,
    string WarehouseName,
    WarehouseDto? Warehouse,
    decimal DeclaredCost,
    decimal CashOnDelivery,
    IReadOnlyList<SenderOption> Senders,
    IReadOnlyList<DropoffPointOption> DropoffPoints,
    IReadOnlyList<SeatPreset> SeatPresets,
    bool CanCreate,
    bool TestMode);

/// <summary>Створити ТТН. SenderId / DropoffPointId = null — типові з налаштувань. CashOnDelivery = 0 — без накладеного платежу.</summary>
public sealed record CreateShipmentRequest(
    string FirstName,
    string LastName,
    string MiddleName,
    string Phone,
    string CityRef,
    string WarehouseRef,
    decimal DeclaredCost,
    decimal CashOnDelivery,
    bool SenderPays,
    IReadOnlyList<ShipmentSeat> Seats,
    string? SenderId = null,
    string? DropoffPointId = null);

/// <summary>Усе, що потрібно API Нової Пошти для нової ТТН (уже перевірене й з нормалізованим телефоном).</summary>
public sealed record NewShipment(
    string SenderId,
    string DropoffPointId,
    int OrderId,
    DateOnly ShipDate,
    string FirstName,
    string LastName,
    string MiddleName,
    string Phone,
    string CityRef,
    string WarehouseRef,
    decimal DeclaredCost,
    decimal CashOnDelivery,
    bool SenderPays,
    IReadOnlyList<ShipmentSeat> Seats);

public sealed record CreatedShipment(string Ref, string Number, decimal Cost);

public enum ShipmentPrintKind
{
    /// <summary>Експрес-накладна.</summary>
    Document,

    /// <summary>Маркування (наліпка на коробку).</summary>
    Markings,
}

/// <summary>Результат оновлення довідника. ClientConfigured = false — ключа немає, довідник не змінено.</summary>
public sealed record WarehouseRefreshResult(bool ClientConfigured, int Count);

/// <summary>Дані замовлення, з яких заповнюється форма ТТН. Prepayment — завдаток, уже отриманий до відправки.</summary>
public sealed record OrderShippingInfo(
    string FirstName, string LastName, string Telephone, string City, string Warehouse, decimal Total, string PaymentCode,
    decimal Prepayment = 0);

/// <summary>ТТН разом з id відправника, від якого її створено (null — створено на старому сайті).</summary>
public sealed record StoredShipment(ShipmentDocument Document, string? SenderId);
