using NewErp.Domain.Common;

namespace NewErp.Domain.Orders;

/// <summary>Рядок нового замовлення: товар, вибрана опція (розмір) і ціна, яку назвав менеджер.</summary>
public sealed record OrderDraftLine(int ProductId, int? ProductOptionValueId, int Quantity, decimal Price)
{
    public decimal Total => Price * Quantity;
}

/// <summary>
/// Замовлення, яке менеджер створює вручну (дзвінок, соцмережі). Перевіряє те саме, що форма швидкого
/// замовлення старого сайту, і рахує підсумки: сума товарів + доставка − знижка.
/// </summary>
public sealed class OrderDraft
{
    private OrderDraft(IReadOnlyList<OrderDraftLine> lines, decimal shippingCost, decimal discount, decimal prepayment)
    {
        Lines = lines;
        ShippingCost = shippingCost;
        Discount = discount;
        Prepayment = prepayment;
    }

    public IReadOnlyList<OrderDraftLine> Lines { get; }

    public decimal ShippingCost { get; }

    public decimal Discount { get; }

    /// <summary>Завдаток, який клієнт уже сплатив (решту, наприклад, бере накладений платіж).</summary>
    public decimal Prepayment { get; }

    public decimal SubTotal => Lines.Sum(l => l.Total);

    public decimal Total => SubTotal + ShippingCost - Discount;

    public static OrderDraft Create(
        string firstname, string telephone, string shippingMethod, string paymentMethod,
        IReadOnlyList<OrderDraftLine> lines, decimal shippingCost, decimal discount, decimal prepayment)
    {
        if (string.IsNullOrWhiteSpace(firstname) && string.IsNullOrWhiteSpace(telephone))
            throw new DomainException("Вкажіть ім'я або телефон клієнта.");
        if (string.IsNullOrWhiteSpace(shippingMethod))
            throw new DomainException("Необхідний спосіб доставки.");
        if (string.IsNullOrWhiteSpace(paymentMethod))
            throw new DomainException("Необхідний спосіб оплати.");
        return ForLines(lines, shippingCost, discount, prepayment);
    }

    /// <summary>
    /// Лише склад і суми: для нового замовлення (через <see cref="Create"/>) і для зміни складу наявного,
    /// де клієнт і способи доставки вже записані.
    /// </summary>
    public static OrderDraft ForLines(IReadOnlyList<OrderDraftLine> lines, decimal shippingCost, decimal discount, decimal prepayment)
    {
        if (lines.Count == 0)
            throw new DomainException("Кошик не може бути порожнім.");
        if (lines.Any(l => l.Quantity <= 0))
            throw new DomainException("Кількість товару має бути більшою за нуль.");
        if (lines.Any(l => l.Price < 0))
            throw new DomainException("Ціна товару не може бути від'ємною.");
        if (shippingCost < 0 || discount < 0 || prepayment < 0)
            throw new DomainException("Доставка, знижка й завдаток не можуть бути від'ємними.");

        var draft = new OrderDraft(lines, shippingCost, discount, prepayment);
        if (draft.Total < 0)
            throw new DomainException("Знижка не може перевищувати суму замовлення.");
        if (prepayment > draft.Total)
            throw new DomainException("Сума завдатку не може перевищувати суму замовлення.");
        return draft;
    }
}

public enum CustomerRiskLevel
{
    /// <summary>Замовлень цього клієнта ще не було.</summary>
    New,
    /// <summary>Є замовлення, скасованих немає.</summary>
    Green,
    /// <summary>Скасовано менше половини замовлень.</summary>
    Yellow,
    /// <summary>Скасовано половину чи більше: варто вимагати передоплату.</summary>
    Red,
}

/// <summary>
/// Наскільки можна довіряти клієнту, за його попередніми замовленнями (як «Fraud Check» старого сайту):
/// що більша частка скасованих, то вищий ризик.
/// </summary>
public sealed record CustomerRisk(CustomerRiskLevel Level, int TotalOrders, IReadOnlyList<int> CancelledOrderIds, decimal SuspicionRate, string Message)
{
    /// <summary>Id статусу «Відмінено» на справжньому сайті.</summary>
    public const int CancelledStatusId = 19;

    /// <summary>Скільки років історії враховується.</summary>
    public const int LookbackYears = 4;

    public static CustomerRisk From(IReadOnlyList<(int OrderId, int StatusId)> orders)
    {
        if (orders.Count == 0)
            return new(CustomerRiskLevel.New, 0, [], 0, "Новий клієнт. Замовлень не знайдено.");

        var cancelled = orders.Where(o => o.StatusId == CancelledStatusId).Select(o => o.OrderId).ToList();
        var rate = Math.Round(cancelled.Count * 100m / orders.Count, 2);

        var (level, message) = cancelled.Count == 0 ? (CustomerRiskLevel.Green, "Клієнт надійний. Скасувань немає.")
            : rate < 50 ? (CustomerRiskLevel.Yellow, "Увага: є скасовані замовлення. Рекомендовано перевірити.")
            : (CustomerRiskLevel.Red, "Потенційний фрод-ризик. Рекомендовано вимагати передоплату.");
        return new(level, orders.Count, cancelled, rate, message);
    }

    /// <summary>Останні 9 цифр телефону: так «+38 (067) 123-45-67» і «0671234567» вважаються одним номером.</summary>
    public static string? PhoneKey(string? telephone)
    {
        var digits = new string((telephone ?? "").Where(char.IsAsciiDigit).ToArray());
        return digits.Length >= 9 ? digits[^9..] : null;
    }
}
