using NewErp.Domain.Sms;

namespace NewErp.Application.Sms;

/// <summary>Журнал SMS (dd_sms_log) і телефон замовлення.</summary>
public interface ISmsLog
{
    /// <returns>Телефон замовлення або null, якщо замовлення немає.</returns>
    Task<string?> GetOrderPhoneAsync(int orderId, CancellationToken ct);

    Task<IReadOnlyList<SmsDto>> ListForOrderAsync(int orderId, CancellationToken ct);

    void Add(SmsMessage message);

    Task SaveChangesAsync(CancellationToken ct);
}
