using NewErp.Domain.Payments;

namespace NewErp.Application.Payments;

/// <summary>Платіж для списку. OrderId = 0 — ще не зіставлено із замовленням.</summary>
public sealed record BankPaymentDto(
    int Id, string AppCode, string Card, decimal Amount, decimal Balance, string Payer, string Description,
    DateTime Timestamp, int OrderId, string? OrderCustomer, bool IsVisible);

/// <param name="Linked">true — лише зіставлені, false — лише незіставлені, null — усі.</param>
/// <param name="Hidden">true — показати приховані замість видимих.</param>
public sealed record BankPaymentFilter(
    DateOnly? From, DateOnly? To, bool? Linked, int? OrderId, string? Search, bool Hidden = false, int Page = 1, int PageSize = 50);

/// <summary>Транзакція з банківської виписки, як її повернув банк.</summary>
public sealed record BankTransaction(
    string Id, string Account, DateOnly Date, TimeOnly Time, decimal Amount, decimal? Balance, bool IsIncoming,
    string Purpose, string Payer, string PayerAccount, string PayerBankCode, string PayerTaxCode);

/// <summary>Підсумок імпорту. ClientConfigured = false — ключі банку не задано, нічого не завантажувалось.</summary>
public sealed record BankImportResult(bool ClientConfigured, int Fetched, int Added, int Skipped);

/// <summary>Замовлення, запропоноване для платежу, і що в ньому збіглося.</summary>
public sealed record PaymentSuggestionDto(
    int OrderId, string Customer, string Telephone, decimal Total, decimal Prepayment, DateTime DateAdded, string Status,
    int? LinkedPaymentId, MatchReason Reasons, int Score);

/// <param name="OrderId">0 — відв'язати платіж від замовлення.</param>
public sealed record LinkPaymentRequest(int OrderId);

/// <summary>Звідки брати кандидатів: замовлення з такою сумою, з такими номерами чи з таким клієнтом за період.</summary>
public sealed record CandidateQuery(
    IReadOnlyList<decimal> Amounts, IReadOnlyList<int> OrderIds, string? Customer, DateTime From, DateTime To, int Limit);
