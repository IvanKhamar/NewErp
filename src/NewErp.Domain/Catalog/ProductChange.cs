using NewErp.Domain.Common;

namespace NewErp.Domain.Catalog;

/// <summary>
/// Що ERP може змінити в товарі магазину: ціну, увімкнення, мітки й категорії. Null — лишається як є. Залишки, порядок, описи
/// й фото тут не змінюються: ними відають виробництво, «Порядок товарів» і адмінка OpenCart. Ті самі правила, що в Java-версії.
/// </summary>
public sealed record ProductChange(decimal? Price, bool? Enabled, IReadOnlyCollection<int>? LabelIds, IReadOnlyCollection<int>? CategoryIds)
{
    /// <param name="knownCategories">Увімкнені категорії плюс ті, в яких товар уже є (вимкнена може лишитися).</param>
    public static ProductChange Of(
        decimal? price, bool? enabled, IEnumerable<int>? labelIds, IEnumerable<int>? categoryIds,
        IReadOnlySet<int> knownLabels, IReadOnlySet<int> knownCategories)
    {
        if (price is { } p && (p < 0 || decimal.Round(p, 2) != p))
            throw new DomainException("Ціна — не менше 0, не більше двох знаків після коми.");
        var labels = labelIds?.Distinct().ToList();
        if (labels is not null && !labels.All(knownLabels.Contains))
            throw new DomainException("Такої мітки немає.");
        var categories = categoryIds?.Distinct().ToList();
        if (categories is not null)
        {
            // Без категорії товар зникає з меню магазину.
            if (categories.Count == 0)
                throw new DomainException("Оберіть хоча б одну категорію.");
            if (!categories.All(knownCategories.Contains))
                throw new DomainException("Такої категорії немає або вона вимкнена.");
        }
        return new ProductChange(price, enabled, labels, categories);
    }
}
