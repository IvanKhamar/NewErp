using NewErp.Application.Common;

namespace NewErp.Application.Catalog;

/// <summary>Товари магазину OpenCart: dd_product і таблиці описів, розмірів, міток і категорій.</summary>
public interface IProductStore
{
    Task<PagedResult<ProductListItem>> ListAsync(ProductFilter filter, CancellationToken ct);

    Task<ProductCardDto?> GetAsync(int productId, CancellationToken ct);

    /// <summary>Усі мітки («Новинка», «Хіт»…) за їхнім порядком.</summary>
    Task<IReadOnlyList<LookupItem>> LabelsAsync(CancellationToken ct);

    /// <summary>Увімкнені категорії за назвою.</summary>
    Task<IReadOnlyList<LookupItem>> CategoriesAsync(CancellationToken ct);

    /// <summary>
    /// Ставить ціну й стан (null — як є) і date_modified = now, лише якщо date_modified досі дорівнює expected.
    /// Інакше (товар змінили чи видалили) — виняток конкурентного доступу (409).
    /// </summary>
    Task UpdateAsync(int productId, DateTime expected, decimal? price, bool? enabled, DateTime now, CancellationToken ct);

    Task ReplaceLabelsAsync(int productId, IReadOnlyCollection<int> labelIds, CancellationToken ct);

    Task ReplaceCategoriesAsync(int productId, IReadOnlyCollection<int> categoryIds, CancellationToken ct);
}
