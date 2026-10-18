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

public sealed record NewOrderLineRecord(OrderDraftLine Line, string Name, string Model, ProductOptionValueDto? Option);
