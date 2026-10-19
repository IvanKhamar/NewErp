using NewErp.Domain.Common;
using NewErp.Domain.Orders;

namespace NewErp.Domain.Customers;

/// <summary>Замовлення клієнта, коротко: для підсумків у картці.</summary>
public sealed record CustomerOrder(int OrderId, DateTime DateAdded, int StatusId, decimal Total);

/// <summary>
/// Підсумки клієнта за його замовленнями. Старий сайт у картці покупця замовлень не показував зовсім,
/// тож скільки клієнт купив, менеджер дивився пошуком у списку замовлень.
/// </summary>
public sealed record CustomerSummary(int OrderCount, int CancelledCount, decimal Spent, DateTime? FirstOrder, DateTime? LastOrder)
{
    /// <summary>Скасовані замовлення («Відмінено») у суму покупок не входять, але лічаться окремо.</summary>
    public static CustomerSummary From(IReadOnlyCollection<CustomerOrder> orders)
    {
        var cancelled = orders.Count(o => o.StatusId == CustomerRisk.CancelledStatusId);
        var spent = orders.Where(o => o.StatusId != CustomerRisk.CancelledStatusId).Sum(o => o.Total);
        return new(
            orders.Count, cancelled, spent,
            orders.Count == 0 ? null : orders.Min(o => o.DateAdded),
            orders.Count == 0 ? null : orders.Max(o => o.DateAdded));
    }
}

/// <summary>Нотатка менеджера про клієнта (dd_customer_history, вкладка «Історія» в картці покупця старого сайту).</summary>
public static class CustomerNote
{
    public const int MaxLength = 2000;

    public static string Validate(string? comment)
    {
        var text = (comment ?? "").Trim();
        if (text.Length == 0)
            throw new DomainException("Напишіть текст нотатки.");
        if (text.Length > MaxLength)
            throw new DomainException($"Нотатка довша за {MaxLength} символів.");
        return text;
    }
}
