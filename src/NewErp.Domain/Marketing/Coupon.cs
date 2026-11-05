using NewErp.Domain.Common;

namespace NewErp.Domain.Marketing;

/// <summary>
/// Перевірений купон, як його перевіряє форма адмінки OpenCart. Type: «P» — відсоток, «F» — сума. Списки кажуть, на що купон
/// діє (порожньо — на весь кошик) і, власне доповнення старого сайту, на що не діє ніколи.
/// </summary>
public sealed record Coupon(
    string Name, string Code, string Type, decimal Discount, decimal Total, bool Logged, bool Shipping, DateOnly Start, DateOnly End,
    int UsesTotal, int UsesCustomer, bool Active, IReadOnlyList<int> ProductIds, IReadOnlyList<int> CategoryIds,
    IReadOnlyList<int> ExcludedProductIds, IReadOnlyList<int> ExcludedCategoryIds)
{
    public const string Percent = "P";
    public const string FixedSum = "F";

    public static Coupon Of(
        string? name, string? code, string? type, decimal? discount, decimal? total, bool logged, bool shipping, DateOnly? start, DateOnly? end,
        int usesTotal, int usesCustomer, bool active, IEnumerable<int>? productIds, IEnumerable<int>? categoryIds,
        IEnumerable<int>? excludedProductIds, IEnumerable<int>? excludedCategoryIds)
    {
        var cleanName = name?.Trim() ?? "";
        if (cleanName.Length is < 3 or > 128)
            throw new DomainException("Назва купона — від 3 до 128 символів.");
        var cleanCode = BirthdayCoupon.RequireCode(code);
        if (type is not (Percent or FixedSum))
            throw new DomainException("Тип знижки — відсоток (P) або сума (F).");
        if (discount is not { } sum || sum <= 0 || decimal.Round(sum, 2) != sum)
            throw new DomainException("Знижка — більше 0, не більше двох знаків після коми.");
        if (type == Percent && sum > 100)
            throw new DomainException("Знижка у відсотках — не більше 100.");
        if (total is not { } minimal || minimal < 0)
            throw new DomainException("Мінімальна сума замовлення не може бути від'ємною.");
        if (start is not { } from || end is not { } to)
            throw new DomainException("Вкажіть, з якого й до якого дня діє купон.");
        if (to < from)
            throw new DomainException("Купон не може закінчитися раніше, ніж почнеться.");
        if (usesTotal < 0 || usesCustomer < 0)
            throw new DomainException("Кількість використань не може бути від'ємною (0 — без обмежень).");

        var (products, categories) = (Ids(productIds), Ids(categoryIds));
        var (excludedProducts, excludedCategories) = (Ids(excludedProductIds), Ids(excludedCategoryIds));
        var both = products.Intersect(excludedProducts).Concat(categories.Intersect(excludedCategories)).ToList();
        if (both.Count > 0)
            throw new DomainException($"№{both.Min()} не може бути водночас серед тих, на які купон діє, і тих, на які не діє.");

        return new Coupon(cleanName, cleanCode, type, sum, minimal, logged, shipping, from, to, usesTotal, usesCustomer, active,
            products, categories, excludedProducts, excludedCategories);
    }

    /// <summary>Додатні номери без повторів, у заданому порядку.</summary>
    private static List<int> Ids(IEnumerable<int>? values) => (values ?? []).Where(v => v > 0).Distinct().ToList();
}
