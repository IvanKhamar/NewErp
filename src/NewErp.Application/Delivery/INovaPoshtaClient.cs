using NewErp.Domain.Delivery;

namespace NewErp.Application.Delivery;

/// <summary>
/// API Нової Пошти. Без ключа в налаштуваннях зареєстровано заглушку, яка нічого не запитує:
/// так у локальній розробці неможливо випадково звернутися до справжнього сервісу.
/// Ключі API відправників знає лише реалізація; назовні — тільки id і назви.
/// </summary>
public interface INovaPoshtaClient
{
    /// <summary>Чи задано ключ, тобто чи справжній це клієнт.</summary>
    bool IsConfigured { get; }

    /// <summary>Чи можна створювати ТТН: справжній клієнт або заглушка з тестовими ТТН (NovaPoshta:FakeDocuments).</summary>
    bool CanCreateDocuments { get; }

    IReadOnlyList<SenderOption> Senders { get; }

    IReadOnlyList<DropoffPointOption> DropoffPoints { get; }

    /// <summary>Статуси ТТН (TrackingDocument.getStatusDocuments). Лише читання: нічого не створює й не змінює.</summary>
    Task<IReadOnlyList<TrackingInfo>> TrackAsync(IReadOnlyList<TrackingRequest> documents, CancellationToken ct);

    /// <summary>Створює отримувача й експрес-накладну (Counterparty.save + InternetDocument.save).</summary>
    Task<CreatedShipment> CreateAsync(NewShipment shipment, CancellationToken ct);

    /// <summary>Видаляє ТТН у Новій Пошті (InternetDocument.delete) ключем того відправника, що її створив.</summary>
    Task DeleteAsync(string senderId, string documentRef, CancellationToken ct);

    /// <summary>PDF накладної чи маркування. Ключ API лишається на сервері, браузер отримує лише файл.</summary>
    Task<byte[]> PrintAsync(string senderId, string documentRef, ShipmentPrintKind kind, CancellationToken ct);

    /// <summary>Увесь довідник відділень і поштоматів (Address.getWarehouses посторінково).</summary>
    Task<IReadOnlyList<NovaPoshtaWarehouse>> FetchWarehousesAsync(CancellationToken ct);
}
