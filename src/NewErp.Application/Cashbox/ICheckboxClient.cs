using NewErp.Domain.Cashbox;

namespace NewErp.Application.Cashbox;

/// <summary>
/// API ПРРО Checkbox. Кожна дія — від імені касира (його логін, пароль і ключ ліцензії каси знає лише реалізація).
/// Без ключів зареєстровано заглушку: вона або нічого не вміє, або (Checkbox:FakeReceipts) видає тестові чеки,
/// тож локальна розробка ніколи не фіскалізує справжній продаж.
/// Відмову Checkbox реалізація перетворює на DomainException з повідомленням сервісу.
/// </summary>
public interface ICheckboxClient
{
    /// <summary>Тестова заглушка, а не справжній сервіс.</summary>
    bool IsFake { get; }

    /// <summary>Чи можна працювати від імені касира: задано його ключі (чи увімкнено тестові чеки).</summary>
    bool CanUse(int cashierId);

    /// <summary>Відкрита зміна касира в Checkbox, а якщо її немає — нова. Повертає id зміни.</summary>
    Task<string> OpenShiftAsync(int cashierId, CancellationToken ct);

    Task<CheckboxShiftState> GetShiftAsync(int cashierId, string shiftId, CancellationToken ct);

    /// <summary>
    /// Закриває зміну касира (Z-звіт). Повертає id Z-звіту, або null, якщо Checkbox ще закриває зміну
    /// (закриття в Checkbox асинхронне; тоді звіт підтягне звірка зміни).
    /// </summary>
    Task<string?> CloseShiftAsync(int cashierId, CancellationToken ct);

    /// <summary>Створює X-звіт (проміжний, зміну не закриває). Повертає id звіту.</summary>
    Task<string> CreateXReportAsync(int cashierId, CancellationToken ct);

    Task<string> GetReportTextAsync(int cashierId, string reportId, CancellationToken ct);

    /// <summary>Фіскалізує чек продажу. Повертає id чека в Checkbox.</summary>
    Task<string> SellAsync(int cashierId, FiscalReceipt receipt, CancellationToken ct);

    /// <summary>Публічна сторінка чека на сервісі Checkbox (ключів не містить); null — у заглушки сторінки немає.</summary>
    string? ReceiptUrl(string receiptId);
}
