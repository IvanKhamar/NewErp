using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Marketing;
using NewErp.Domain.Orders;

namespace NewErp.Application.Orders;

/// <summary>
/// Швидке створення замовлення менеджером (замовлення за дзвінком чи із соцмереж), як форма «Швидке замовлення»
/// старого сайту, і перевірка, чи не скасовував клієнт попередні замовлення.
/// </summary>
public sealed class NewOrderService(INewOrderStore store, IOrderStore orders, OrderService orderService, TimeProvider clock)
{
    private const int SearchLimit = 10;

    public Task<IReadOnlyList<ProductPickDto>> SearchProductsAsync(string? term, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(term) ? Task.FromResult<IReadOnlyList<ProductPickDto>>([]) : store.SearchProductsAsync(term.Trim(), SearchLimit, ct);

    public Task<IReadOnlyList<CustomerPickDto>> SearchCustomersAsync(string? term, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(term) || term.Trim().Length < 3
            ? Task.FromResult<IReadOnlyList<CustomerPickDto>>([])
            : store.SearchCustomersAsync(term.Trim(), SearchLimit, ct);

    public Task<OrderMethodsDto> ListMethodsAsync(CancellationToken ct) => store.ListMethodsAsync(ct);

    public async Task<CustomerRiskDto> CheckRiskAsync(CustomerRiskQuery query, CancellationToken ct)
    {
        var phoneKey = CustomerRisk.PhoneKey(query.Telephone);
        var firstname = query.Firstname?.Trim();
        var lastname = query.Lastname?.Trim();
        var hasName = !string.IsNullOrEmpty(firstname) && !string.IsNullOrEmpty(lastname);
        var email = query.Email?.Trim();

        if (phoneKey is null && !hasName && string.IsNullOrEmpty(email))
            return ToDto(CustomerRisk.From([]));

        var since = Now().AddYears(-CustomerRisk.LookbackYears);
        var found = await store.FindCustomerOrdersAsync(
            phoneKey, hasName ? firstname : null, hasName ? lastname : null, string.IsNullOrEmpty(email) ? null : email, since, ct);
        return ToDto(CustomerRisk.From(found));
    }

    public async Task<CreateOrderResult> CreateAsync(CreateOrderRequest request, int userId, CancellationToken ct)
    {
        var lines = (request.Lines ?? []).Select(l => new OrderDraftLine(l.ProductId, l.ProductOptionValueId, l.Quantity, l.Price)).ToList();
        var draft = OrderDraft.Create(
            request.Firstname, request.Telephone, request.ShippingMethod, request.PaymentMethod,
            lines, request.ShippingCost, request.Discount, request.Prepayment);

        if (!await orders.StatusExistsAsync(request.StatusId, ct))
            throw new DomainException($"Статусу {request.StatusId} не існує.");
        if (request.OrderSourceId != 0 && !await store.SourceExistsAsync(request.OrderSourceId, ct))
            throw new DomainException($"Джерела замовлення {request.OrderSourceId} не існує.");
        var managerId = request.ManagerId ?? userId;
        if (!await orders.IsActiveUserAsync(managerId, ct))
            throw new DomainException($"Користувача {managerId} не знайдено або його вимкнено.");

        var products = (await store.GetProductsAsync(lines.Select(l => l.ProductId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);
        var records = lines.Select(line => ToRecord(line, products)).ToList();

        // Ризик рахуємо до збереження, щоб нове замовлення не враховувало саме себе.
        var risk = await CheckRiskAsync(new CustomerRiskQuery(request.Telephone, request.Firstname, request.Lastname, request.Email), ct);

        // Як на старому сайті: спершу замовлення без статусу, потім статус через звичайну зміну статусу,
        // щоб спрацювали журнал і обробники інших модулів (списання виробів тощо).
        var now = Now();
        var orderId = await store.CreateAsync(new NewOrderRecord(request, draft, records, now), ct);
        await orders.SaveManagerAsync(orderId, managerId, userId, now, ct);
        var status = await orderService.ChangeStatusAsync(orderId, new ChangeOrderStatusRequest(request.StatusId), userId, ct);

        return new CreateOrderResult(orderId, status.Warnings, risk);
    }

    public async Task<OrderCompositionDto> GetCompositionAsync(int orderId, CancellationToken ct) =>
        await store.GetCompositionAsync(orderId, ct) ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");

    /// <summary>
    /// Нові товари, розміри, кількість і суми замовлення (форма «Швидке замовлення» старого сайту для наявного замовлення).
    /// Пошиті вироби перераховуються за новим складом, якщо статус замовлення їх списує.
    /// </summary>
    public async Task<UpdateOrderCompositionResult> UpdateCompositionAsync(int orderId, UpdateOrderCompositionRequest request, int userId, CancellationToken ct)
    {
        var current = await GetCompositionAsync(orderId, ct);
        var lines = (request.Lines ?? []).Select(l => new OrderDraftLine(l.ProductId, l.ProductOptionValueId, l.Quantity, l.Price)).ToList();
        var draft = OrderDraft.ForLines(lines, request.ShippingCost, request.Discount, request.Prepayment);
        if (!await orders.IsActiveUserAsync(userId, ct))
            throw new DomainException($"Користувача {userId} не знайдено або його вимкнено.");

        var products = (await store.GetProductsAsync(lines.Select(l => l.ProductId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);
        var records = lines.Select(line => ToRecord(line, products)).ToList();

        // Списаний у замовленні сертифікат лишається: нова сума має покрити і його, і завдаток.
        var voucher = await store.VoucherPaidAsync(orderId, ct);
        var total = draft.Total - voucher;
        if (total < 0)
            throw new DomainException($"Сума замовлення стала меншою за оплату сертифікатом ({GiftVoucher.Plain(voucher)} грн). Спершу зніміть сертифікат.");
        if (draft.Prepayment > total)
            throw new DomainException("Сума завдатку не може перевищувати суму замовлення.");

        var warnings = await orderService.ReapplyStatusAsync(
            orderId, current.StatusId, () => store.ReplaceLinesAsync(orderId, draft, records, Now(), ct), userId, ct);
        return new UpdateOrderCompositionResult(total, warnings);
    }

    private static NewOrderLineRecord ToRecord(OrderDraftLine line, Dictionary<int, ProductPickDto> products)
    {
        if (!products.TryGetValue(line.ProductId, out var product))
            throw new DomainException($"Товару {line.ProductId} не знайдено.");

        ProductOptionValueDto? option = null;
        if (line.ProductOptionValueId is { } valueId)
            option = product.Options.FirstOrDefault(o => o.ProductOptionValueId == valueId)
                ?? throw new DomainException($"Товар «{product.Name}» не має такого розміру чи опції ({valueId}).");
        else if (product.Options.Count > 0)
            throw new DomainException($"Оберіть розмір чи опцію для товару «{product.Name}».");

        return new NewOrderLineRecord(line, product.Name, product.Model, option);
    }

    private static CustomerRiskDto ToDto(CustomerRisk risk) =>
        new(risk.Level, risk.TotalOrders, risk.CancelledOrderIds.Count, risk.CancelledOrderIds, risk.SuspicionRate, risk.Message);

    private DateTime Now() => clock.GetLocalNow().DateTime;
}
