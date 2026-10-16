using NewErp.Domain.Sms;

namespace NewErp.Application.Sms;

/// <summary>Журнал SMS (dd_sms_log) і телефон замовлення.</summary>
public interface ISmsLog
{
    /// <returns>Телефон замовлення або null, якщо замовлення немає.</returns>
    Task<string?> GetOrderPhoneAsync(int orderId, CancellationToken ct);

    Task<IReadOnlyList<SmsDto>> ListForOrderAsync(int orderId, CancellationToken ct);

    /// <summary>Якому замовленню належить кожна з ТТН (номер → id замовлення); невідомих номерів у відповіді немає.</summary>
    Task<IReadOnlyDictionary<string, int>> FindOrderIdsByTtnAsync(IReadOnlyCollection<string> numbers, CancellationToken ct);

    void Add(SmsMessage message);

    Task SaveChangesAsync(CancellationToken ct);
}
