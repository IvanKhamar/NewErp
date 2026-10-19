using NewErp.Domain.Common;

namespace NewErp.Domain.Returns;

/// <summary>Рядок замовлення, з якого оформлюють повернення.</summary>
public sealed record ReturnableLine(int ProductId, string Name, string Model, int Quantity);

/// <summary>
/// Повернення товару (dd_return), як у «Продажі → Повернення» старого сайту: клієнт, товар, кількість, причина,
/// дія (повернено кошти, обмін…) і статус з журналом. Склад і гроші старий модуль не змінював.
/// </summary>
public static class ProductReturn
{
    public const int MaxCommentLength = 2000;

    /// <summary>
    /// Перевірки форми старого сайту (ім'я й прізвище до 32 символів, телефон 3–32, причина обов'язкова) плюс те,
    /// чого він не перевіряв: товар має бути в замовленні, а повернути можна не більше, ніж куплено мінус уже оформлені повернення.
    /// E-mail необов'язковий (старий сайт вимагав його, хоча замовлення за дзвінком часто без пошти), але якщо є — до 96 символів.
    /// </summary>
    public static ReturnableLine Validate(
        string firstname, string lastname, string telephone, string email, IReadOnlyList<ReturnableLine> orderLines,
        int productId, int quantity, int reasonId, string? comment, int alreadyReturned)
    {
        if (firstname.Trim().Length is < 1 or > 32)
            throw new DomainException("Ім'я клієнта має містити від 1 до 32 символів.");
        if (lastname.Trim().Length > 32)
            throw new DomainException("Прізвище клієнта має містити до 32 символів.");
        if (telephone.Trim().Length is < 3 or > 32)
            throw new DomainException("Телефон клієнта має містити від 3 до 32 символів.");
        if (email.Trim().Length > 96)
            throw new DomainException("E-mail занадто довгий.");
        if (reasonId <= 0)
            throw new DomainException("Оберіть причину повернення.");
        if ((comment ?? "").Length > MaxCommentLength)
            throw new DomainException($"Коментар довший за {MaxCommentLength} символів.");

        var line = orderLines.FirstOrDefault(l => l.ProductId == productId)
            ?? throw new DomainException("Цього товару немає в замовленні.");
        var left = orderLines.Where(l => l.ProductId == productId).Sum(l => l.Quantity) - alreadyReturned;
        if (left <= 0)
            throw new DomainException($"«{line.Name}» з цього замовлення вже повернено повністю.");
        if (quantity <= 0 || quantity > left)
            throw new DomainException($"Кількість має бути від 1 до {left}.");
        return line;
    }
}
