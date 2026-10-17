using System.Text.RegularExpressions;

namespace NewErp.Domain.Payments;

/// <summary>Замовлення-кандидат для зіставлення з платежем.</summary>
public sealed record OrderCandidate(
    int OrderId, string Customer, string Telephone, decimal Total, decimal Prepayment, DateTime DateAdded, string Status, int? LinkedPaymentId);

/// <summary>Чому замовлення запропоновано: що в ньому збіглося з платежем.</summary>
[Flags]
public enum MatchReason
{
    None = 0,
    /// <summary>Номер замовлення є в призначенні платежу.</summary>
    OrderNumber = 1,
    /// <summary>Сума платежу дорівнює сумі замовлення.</summary>
    Total = 2,
    /// <summary>Сума платежу дорівнює завдатку чи залишку після нього.</summary>
    Prepayment = 4,
    /// <summary>Телефон клієнта є в призначенні платежу.</summary>
    Phone = 8,
    /// <summary>Прізвище клієнта є в призначенні чи імені платника.</summary>
    Name = 16,
}

public sealed record ScoredCandidate(OrderCandidate Order, MatchReason Reasons, int Score);

/// <summary>
/// Підказує, якому замовленню належить платіж. Старий сайт шукав замовлення лише з такою ж сумою (без копійок)
/// і показував перші п'ять; тут ще враховуються номер замовлення, телефон і прізвище в призначенні платежу.
/// </summary>
public static class PaymentMatcher
{
    /// <summary>Номери замовлень у призначенні: окремі числа з 3–7 цифр («зам. 12345», «№12345»), але не суми («1500.00»).</summary>
    public static IReadOnlyList<int> OrderNumbersIn(string text) =>
        Regex.Matches(text ?? "", @"(?<![\d.,])\d{3,7}(?!\d|[.,]\d)")
            .Select(m => int.Parse(m.Value))
            .Distinct()
            .ToList();

    public static IReadOnlyList<ScoredCandidate> Rank(BankPayment payment, IEnumerable<OrderCandidate> candidates)
    {
        var text = $"{payment.Description} {payment.Terminal}";
        var digits = Regex.Replace(text, @"\D", "");
        var numbers = OrderNumbersIn(text).ToHashSet();
        var lower = text.ToLowerInvariant();

        return candidates
            .Select(c =>
            {
                var reasons = MatchReason.None;
                if (numbers.Contains(c.OrderId))
                    reasons |= MatchReason.OrderNumber;
                if (c.Total == payment.Amount)
                    reasons |= MatchReason.Total;
                else if (c.Prepayment > 0 && (c.Prepayment == payment.Amount || c.Total - c.Prepayment == payment.Amount))
                    reasons |= MatchReason.Prepayment;
                var phone = Regex.Replace(c.Telephone, @"\D", "");
                if (phone.Length >= 9 && digits.Contains(phone[^9..]))
                    reasons |= MatchReason.Phone;
                var surname = c.Customer.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (surname is { Length: >= 3 } && lower.Contains(surname.ToLowerInvariant()))
                    reasons |= MatchReason.Name;

                // Номер і телефон майже однозначні, сума — ні (однакових сум багато). Уже оплачені іншим платежем — нижче.
                var score = (reasons.HasFlag(MatchReason.OrderNumber) ? 50 : 0)
                    + (reasons.HasFlag(MatchReason.Phone) ? 30 : 0)
                    + (reasons.HasFlag(MatchReason.Total) ? 25 : 0)
                    + (reasons.HasFlag(MatchReason.Prepayment) ? 20 : 0)
                    + (reasons.HasFlag(MatchReason.Name) ? 15 : 0)
                    - (c.LinkedPaymentId is not null && c.LinkedPaymentId != payment.Id ? 40 : 0);
                return new ScoredCandidate(c, reasons, score);
            })
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Order.DateAdded)
            .ToList();
    }
}
