using NewErp.Domain.Catalog;

namespace NewErp.Application.Catalog;

public interface IProductSortStore
{
    /// <summary>Усі групи у порядку перерахунку: теми, категорії (з кольорами окремо), мітки, панель допродажу.</summary>
    Task<IReadOnlyList<SortGroup>> ListGroupsAsync(CancellationToken ct);

    /// <summary>
    /// Увімкнені товари групи у вихідному порядку: для мітки — від найновішого, для панелі допродажу — за її поточним порядком,
    /// для решти — за id.
    /// </summary>
    Task<IReadOnlyList<int>> GroupProductIdsAsync(SortGroup group, CancellationToken ct);

    /// <summary>Продано штук кожного товару за період (лише замовлення, що рахуються). crossSellOnly — лише додані через панель допродажу.</summary>
    Task<IReadOnlyDictionary<int, int>> SalesAsync(IReadOnlyCollection<int> productIds, DateTime from, DateTime to, bool crossSellOnly, CancellationToken ct);

    /// <summary>Назва, фото й поточний порядок товарів (для панелі допродажу — її власний, без нього 999999).</summary>
    Task<IReadOnlyList<SortProductDto>> ProductsAsync(IReadOnlyCollection<int> productIds, bool crossSell, CancellationToken ct);

    /// <summary>Записує порядок 1, 2, 3… у dd_product.sort_order або, для панелі, замінює dd_crosssell_sort.</summary>
    Task<int> ApplyAsync(bool crossSell, IReadOnlyList<int> orderedProductIds, CancellationToken ct);

    Task<IReadOnlyList<SortPin>> PinsAsync(SortGroup group, CancellationToken ct);

    Task<IReadOnlyList<SortPinDto>> ListPinsAsync(CancellationToken ct);

    /// <summary>Закріплює товар на позиції; попередній пін цього товару чи цієї позиції в групі прибирається.</summary>
    Task SetPinAsync(SortGroup group, int position, int productId, DateTime now, CancellationToken ct);

    Task<bool> RemovePinAsync(int id, CancellationToken ct);

    /// <summary>Зберігає поточний sort_order усіх товарів.</summary>
    Task<int> CreateSnapshotAsync(string note, DateTime now, CancellationToken ct);

    Task<IReadOnlyList<SortSnapshotDto>> ListSnapshotsAsync(int limit, CancellationToken ct);

    Task<bool> RestoreSnapshotAsync(int id, CancellationToken ct);

    Task<bool> CrossSellPanelOnlyAsync(CancellationToken ct);

    Task SetCrossSellPanelOnlyAsync(bool panelOnly, CancellationToken ct);

    /// <summary>Увімкнені товари, у назві яких є term.</summary>
    Task<IReadOnlyList<SortProductDto>> SearchProductsAsync(string term, int limit, CancellationToken ct);
}
