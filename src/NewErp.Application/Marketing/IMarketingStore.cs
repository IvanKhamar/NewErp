using NewErp.Domain.Marketing;

namespace NewErp.Application.Marketing;

public interface IMarketingStore
{
    /// <summary>Усі замовлення, де вказано день і місяць народження.</summary>
    Task<IReadOnlyList<BirthdayOrder>> BirthdayOrdersAsync(CancellationToken ct);

    /// <summary>Номери з журналу SMS, яким надсилали привітання починаючи з since.</summary>
    Task<IReadOnlyList<string>> BirthdaySmsPhonesAsync(DateTime since, CancellationToken ct);

    Task<bool> CouponExistsAsync(string code, CancellationToken ct);

    Task<int> AddCouponAsync(CouponDraft coupon, CancellationToken ct);

    /// <summary>Id і телефон кожного оформленого замовлення (для пошуку замовлень клієнта за номером).</summary>
    Task<IReadOnlyList<(int OrderId, string Phone)>> OrderPhonesAsync(CancellationToken ct);

    Task<IReadOnlyList<CustomerOrderDto>> OrdersAsync(IReadOnlyCollection<int> orderIds, CancellationToken ct);

    /// <summary>Непорожні значення поля з вибраних джерел за період (сирі, ще не очищені).</summary>
    Task<IReadOnlyList<string>> ContactValuesAsync(ContactExportRequest request, CancellationToken ct);

    /// <summary>Контактні дані всіх оформлених замовлень.</summary>
    Task<IReadOnlyList<OrderContact>> OrderContactsAsync(CancellationToken ct);
}
