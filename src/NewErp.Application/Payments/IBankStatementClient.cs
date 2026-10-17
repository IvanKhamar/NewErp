namespace NewErp.Application.Payments;

/// <summary>
/// Виписка ПриватБанку (Автоклієнт для бізнес-рахунків). Без ключів зареєстровано заглушку, яка нічого не запитує.
/// Ключі (id і token кожного рахунку) знає лише реалізація.
/// </summary>
public interface IBankStatementClient
{
    bool IsConfigured { get; }

    /// <summary>Усі транзакції за період (і надходження, і списання) з усіх налаштованих рахунків.</summary>
    Task<IReadOnlyList<BankTransaction>> FetchAsync(DateOnly from, DateOnly to, CancellationToken ct);
}
