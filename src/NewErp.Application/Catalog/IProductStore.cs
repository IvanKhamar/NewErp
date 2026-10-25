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
}
