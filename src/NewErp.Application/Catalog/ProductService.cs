using NewErp.Application.Common;

namespace NewErp.Application.Catalog;

/// <summary>
/// Товари магазину, як «Каталог → Товари» старого сайту. Нові товари, описи, фото й SEO-адреси лишаються в адмінці OpenCart;
/// залишки й порядок товарів належать виробництву й «Порядку товарів».
/// </summary>
public sealed class ProductService(IProductStore store)
{
    public Task<PagedResult<ProductListItem>> ListAsync(ProductFilter filter, CancellationToken ct)
    {
        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize);
        return store.ListAsync(filter with { Search = filter.Search?.Trim(), Page = page, PageSize = pageSize }, ct);
    }

    public async Task<ProductCardDto> GetAsync(int productId, CancellationToken ct) =>
        await store.GetAsync(productId, ct) ?? throw new NotFoundException($"Товар {productId} не знайдено.");

    public Task<IReadOnlyList<LookupItem>> LabelsAsync(CancellationToken ct) => store.LabelsAsync(ct);

    public Task<IReadOnlyList<LookupItem>> CategoriesAsync(CancellationToken ct) => store.CategoriesAsync(ct);
}
