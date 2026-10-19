using NewErp.Domain.Orders;

namespace NewErp.Application.Orders;

public sealed record NewOrderLineRequest(int ProductId, int? ProductOptionValueId, int Quantity, decimal Price);

/// <summary>Замовлення, яке менеджер створює вручну. ManagerId не вказано — відповідальним стає той, хто створює.</summary>
public sealed record CreateOrderRequest(
    string Firstname,
    string Lastname,
    string Telephone,
    string Email,
    string Instagram,
    int OrderSourceId,
    int StatusId,
    int? ManagerId,
    string ShippingCode,
    string ShippingMethod,
    string City,
    string Address1,
    string Address2,
    string House,
    string Flat,
    string PaymentCode,
    string PaymentMethod,
    IReadOnlyList<NewOrderLineRequest> Lines,
    decimal ShippingCost = 0,
    decimal Discount = 0,
    decimal Prepayment = 0,
    string Comment = "");

/// <summary>Warnings — попередження інших модулів після встановлення статусу (наприклад, нестача виробів).</summary>
public sealed record CreateOrderResult(int OrderId, IReadOnlyList<string> Warnings, CustomerRiskDto Risk);

public sealed record ProductOptionValueDto(int ProductOptionId, int ProductOptionValueId, string OptionName, string Value, int Quantity, decimal Price);

/// <summary>Товар для додавання в замовлення. Price — базова ціна; ціна опції, якщо вона більша за 0, її замінює.</summary>
public sealed record ProductPickDto(int Id, string Name, string Model, decimal Price, int Quantity, IReadOnlyList<ProductOptionValueDto> Options);

/// <summary>Контакти клієнта з його останнього замовлення.</summary>
public sealed record CustomerPickDto(
    int CustomerId, string Firstname, string Lastname, string Telephone, string Email, string Instagram,
    string City, string Address1, string Address2, int LastOrderId);

public sealed record CustomerRiskQuery(string? Telephone = null, string? Firstname = null, string? Lastname = null, string? Email = null);

public sealed record CustomerRiskDto(
    CustomerRiskLevel Level, int TotalOrders, int CancelledOrders, IReadOnlyList<int> CancelledOrderIds, decimal SuspicionRate, string Message);

/// <summary>Спосіб доставки чи оплати: код (як у OpenCart) і назва.</summary>
public sealed record OrderMethodDto(string Code, string Name);

public sealed record OrderMethodsDto(IReadOnlyList<OrderMethodDto> Shipping, IReadOnlyList<OrderMethodDto> Payment);

/// <summary>Те, що треба записати в базу для нового замовлення (усі перевірки вже пройдено).</summary>
public sealed record NewOrderRecord(
    CreateOrderRequest Request,
    OrderDraft Draft,
    IReadOnlyList<NewOrderLineRecord> Lines,
    DateTime CreatedAt);

/// <summary>
/// Рядок складу замовлення. ProductOptionValueId — вибраний розмір чи опція (null, якщо товар без опцій);
/// Options — розміри товару в каталозі зараз, щоб розмір можна було змінити.
/// </summary>
public sealed record OrderCompositionLine(
    int ProductId, int? ProductOptionValueId, string Name, string Model, string Option, int Quantity, decimal Price,
    IReadOnlyList<ProductOptionValueDto> Options);

/// <summary>Склад замовлення для редагування: товари й суми (доставка, знижка, завдаток) з підсумків OpenCart.</summary>
public sealed record OrderCompositionDto(
    int OrderId, int StatusId, IReadOnlyList<OrderCompositionLine> Lines, decimal ShippingCost, decimal Discount, decimal Prepayment, decimal Total);

/// <summary>Новий склад замовлення повністю замінює старий (як редагування замовлення на старому сайті).</summary>
public sealed record UpdateOrderCompositionRequest(
    IReadOnlyList<NewOrderLineRequest> Lines, decimal ShippingCost = 0, decimal Discount = 0, decimal Prepayment = 0);

/// <summary>Warnings — попередження складу (наприклад, бракує пошитих виробів для нових рядків).</summary>
public sealed record UpdateOrderCompositionResult(decimal Total, IReadOnlyList<string> Warnings);

public sealed record NewOrderLineRecord(OrderDraftLine Line, string Name, string Model, ProductOptionValueDto? Option);
