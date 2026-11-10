namespace NewErp.Application.Orders;

// Інструменти сторінки замовлення старої адмінки (пункти 35–42 плану Python-версії, перенесено звідти).

/// <summary>Покупець, адреса й способи доставки та оплати замовлення, як їх показує форма редагування.</summary>
public sealed record OrderCustomerDto(
    int OrderId,
    string Firstname,
    string Lastname,
    string Telephone,
    string Email,
    string Instagram,
    string ShippingCode,
    string ShippingMethod,
    string City,
    string Address1,
    string Address2,
    string House,
    string Flat,
    string PaymentCode,
    string PaymentMethod,
    string Comment);

/// <summary>
/// Замінює ці поля; адреса оплати така сама, як доставки, як у новому замовленні. Довжини — колонок OpenCart,
/// тож нічого не обрізається мовчки.
/// </summary>
public sealed record UpdateOrderCustomerRequest(
    string? Firstname = null,
    string? Lastname = null,
    string? Telephone = null,
    string? Email = null,
    string? Instagram = null,
    string? ShippingCode = null,
    string? ShippingMethod = null,
    string? City = null,
    string? Address1 = null,
    string? Address2 = null,
    string? House = null,
    string? Flat = null,
    string? PaymentCode = null,
    string? PaymentMethod = null,
    string? Comment = null);

/// <summary>null прибирає дату.</summary>
public sealed record ShippingDateRequest(DateOnly? DateShipping = null);

public sealed record InvoiceDto(string Invoice);

public sealed record ShippingCostLine(int ProductId = 0, int Quantity = 1);

/// <summary>Cost — оголошена вартість (сума товарів), як її надсилало швидке замовлення старого сайту.</summary>
public sealed record ShippingCostRequest(string City = "", string ShippingCode = "", IReadOnlyList<ShippingCostLine>? Lines = null, decimal Cost = 0);

public sealed record ShippingCostDto(decimal Cost);

public sealed record ShippingIssueDto(string Level, string Text);

/// <summary>Issues порожній, коли статус можна ставити без запитань.</summary>
public sealed record ShippingCheckDto(int OrderId, int StatusId, string Customer, string Telephone, string Waybill, IReadOnlyList<ShippingIssueDto> Issues);

public sealed record DiscrepancyDto(int OrderId, string Customer, string Level, string Text);

public sealed record DiscrepancyGroupDto(string Title, IReadOnlyList<DiscrepancyDto> Items);

public sealed record OrderCreditsDto(int OrderId, int CustomerId, int RewardPoints, bool RewardAdded, int AffiliateId, decimal Commission, bool CommissionAdded);

// ── Друк: рахунок і пакувальний лист (кнопки друку старої адмінки) ──

/// <summary>Реквізити магазину з налаштувань OpenCart.</summary>
public sealed record PrintStoreDto(string Name, string Address, string Telephone, string Email);

/// <summary>Options: «Розмір: M».</summary>
public sealed record PrintLineDto(string Name, string Model, string Options, int Quantity, decimal Price, decimal Total);

public sealed record PrintTotalDto(string Title, decimal Value);

public sealed record PrintOrderDto(
    int Id,
    string? Invoice,
    DateTime DateAdded,
    string Customer,
    string Telephone,
    string Email,
    string ShippingMethod,
    string ShippingAddress,
    string PaymentMethod,
    string Comment,
    string CurrencyCode,
    PrintStoreDto Store,
    IReadOnlyList<PrintLineDto> Lines,
    IReadOnlyList<PrintTotalDto> Totals);
