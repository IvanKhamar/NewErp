using NewErp.Application.Common;
using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

namespace NewErp.Application.Catalog;

/// <summary>
/// Товари магазину, як «Каталог → Товари» старого сайту: ERP змінює ціну, увімкнення, мітки й категорії. Нові товари, описи,
/// фото й SEO-адреси лишаються в адмінці OpenCart; залишки й порядок товарів належать виробництву й «Порядку товарів».
/// </summary>
public sealed class ProductService(IProductStore store, TimeProvider clock)
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

    /// <summary>
    /// Змінює ціну, увімкнення, мітки й категорії. Таблиці OpenCart — MyISAM, без транзакцій, тож відкату немає: спершу
    /// захищений UPDATE товару, а мітки й категорії — лише після нього.
    /// </summary>
    public async Task<ProductCardDto> UpdateAsync(int productId, UpdateProductRequest request, CancellationToken ct)
    {
        var current = await GetAsync(productId, ct);
        if (request.DateModified is not { } expected)
            throw new DomainException("Не вказано dateModified: з якою версією товару ви працювали.");
        var knownLabels = (await store.LabelsAsync(ct)).Select(l => l.Id).ToHashSet();
        // Категорія, яку потім вимкнули, може лишитися в товарі, але обрати її наново не можна.
        var knownCategories = (await store.CategoriesAsync(ct)).Select(c => c.Id).Concat(current.Categories.Select(c => c.Id)).ToHashSet();
        var change = ProductChange.Of(request.Price, request.Enabled, request.LabelIds, request.CategoryIds, knownLabels, knownCategories);

        // date_modified — datetime без часток секунди, як його пише OpenCart.
        var now = clock.GetLocalNow().DateTime;
        await store.UpdateAsync(productId, expected, change.Price, change.Enabled, now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond)), ct);
        if (change.LabelIds is { } labels)
            await store.ReplaceLabelsAsync(productId, labels, ct);
        if (change.CategoryIds is { } categories)
            await store.ReplaceCategoriesAsync(productId, categories, ct);
        return await GetAsync(productId, ct);
    }
}
