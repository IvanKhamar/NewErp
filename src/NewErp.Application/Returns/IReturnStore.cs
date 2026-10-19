using NewErp.Application.Common;

namespace NewErp.Application.Returns;

public interface IReturnStore
{
    Task<PagedResult<ReturnListItem>> ListAsync(ReturnFilter filter, CancellationToken ct);

    Task<ReturnDetails?> GetAsync(int returnId, CancellationToken ct);

    Task<ReturnLookupsDto> LookupsAsync(CancellationToken ct);

    /// <summary>Замовлення з товарами й тим, скільки кожного товару вже оформлено на повернення; null, якщо замовлення немає.</summary>
    Task<ReturnSourceOrder?> GetSourceOrderAsync(int orderId, CancellationToken ct);

    /// <summary>Записує повернення й перший запис журналу. Повертає id.</summary>
    Task<int> CreateAsync(NewReturnRecord record, CancellationToken ct);

    /// <summary>Як addReturnHistory старого сайту: новий статус (і дія, якщо задано) та запис у журнал.</summary>
    Task<bool> AddHistoryAsync(int returnId, int statusId, string comment, int? actionId, DateTime now, CancellationToken ct);
}
