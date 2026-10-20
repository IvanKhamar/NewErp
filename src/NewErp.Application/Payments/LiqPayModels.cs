using NewErp.Domain.Payments;

namespace NewErp.Application.Payments;

/// <summary>Amount не задано — посилання на всю суму замовлення.</summary>
public sealed record CreatePaymentLinkRequest(decimal? Amount);

/// <summary>IsTest: ключів LiqPay не налаштовано, посилання тестове й гроші за ним не прийдуть.</summary>
public sealed record PaymentLinkDto(string Url, decimal Amount, string Currency, bool IsTest);

public sealed record LiqPayStatusDto(LiqPayState State, string Label, decimal? Amount, DateTime? PaidAt, bool IsTest);

/// <summary>Що передаємо в LiqPay для оплати: номер замовлення сайту, щоб наявний callback старого сайту його впізнав.</summary>
public sealed record LiqPayCheckout(string OrderId, decimal Amount, string Currency, string Description);

/// <summary>Сира відповідь LiqPay на запит статусу.</summary>
public sealed record LiqPayStatusReply(string? Status, string? ErrorCode, decimal? Amount, DateTime? EndDate);

/// <summary>
/// LiqPay (API версії 3). Ключі (public і private) знає лише реалізація; без них зареєстровано заглушку,
/// яка будує тестове посилання й нічого не запитує.
/// </summary>
public interface ILiqPayGateway
{
    bool IsTest { get; }

    string CheckoutUrl(LiqPayCheckout checkout);

    Task<LiqPayStatusReply> StatusAsync(string orderId, CancellationToken ct);
}
