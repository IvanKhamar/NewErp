using NewErp.Domain.Orders;

namespace NewErp.Application.Orders;

/// <summary>Те, що потрібно для створення замовлення вручну: товари, клієнти, способи доставки й оплати.</summary>
public interface INewOrderStore
{
    /// <summary>Увімкнені товари, у назві чи моделі яких є term (або з таким id).</summary>
    Task<IReadOnlyList<ProductPickDto>> SearchProductsAsync(string term, int limit, CancellationToken ct);

    Task<IReadOnlyList<ProductPickDto>> GetProductsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);

    /// <summary>Клієнти з попередніх замовлень, у яких ім'я, прізвище, телефон чи email містить term.</summary>
    Task<IReadOnlyList<CustomerPickDto>> SearchCustomersAsync(string term, int limit, CancellationToken ct);

    /// <summary>
    /// Замовлення за останні роки, що збігаються з клієнтом за телефоном (останні 9 цифр), за ім'ям і прізвищем
    /// разом або за email.
    /// </summary>
    Task<IReadOnlyList<(int OrderId, int StatusId)>> FindCustomerOrdersAsync(
        string? phoneKey, string? firstname, string? lastname, string? email, DateTime since, CancellationToken ct);

    /// <summary>Способи доставки й оплати, які вже траплялись у замовленнях (від найчастішого).</summary>
    Task<OrderMethodsDto> ListMethodsAsync(CancellationToken ct);

    Task<bool> SourceExistsAsync(int sourceId, CancellationToken ct);

    /// <summary>Записує замовлення без статусу (order_status_id = 0), його товари, опції й підсумки.</summary>
    /// <returns>Id нового замовлення.</returns>
    Task<int> CreateAsync(NewOrderRecord order, CancellationToken ct);

    /// <summary>Склад замовлення або null, якщо замовлення немає.</summary>
    Task<OrderCompositionDto?> GetCompositionAsync(int orderId, CancellationToken ct);

    /// <summary>Замінює товари, опції й підсумки замовлення, оновлює суму й завдаток.</summary>
    Task ReplaceLinesAsync(int orderId, OrderDraft draft, IReadOnlyList<NewOrderLineRecord> lines, DateTime now, CancellationToken ct);
}
