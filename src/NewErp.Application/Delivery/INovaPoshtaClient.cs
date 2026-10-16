using NewErp.Domain.Delivery;

namespace NewErp.Application.Delivery;

/// <summary>
/// API Нової Пошти. Без ключа в налаштуваннях зареєстровано заглушку, яка нічого не запитує:
/// так у локальній розробці неможливо випадково звернутися до справжнього сервісу.
/// </summary>
public interface INovaPoshtaClient
{
    /// <summary>Чи задано ключ, тобто чи справжній це клієнт.</summary>
    bool IsConfigured { get; }

    /// <summary>Статуси ТТН (TrackingDocument.getStatusDocuments). Лише читання: нічого не створює й не змінює.</summary>
    Task<IReadOnlyList<TrackingInfo>> TrackAsync(IReadOnlyList<TrackingRequest> documents, CancellationToken ct);
}
