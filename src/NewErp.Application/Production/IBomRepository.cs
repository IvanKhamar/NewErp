using NewErp.Domain.Production;

namespace NewErp.Application.Production;

public interface IBomRepository
{
    Task<IReadOnlyList<BomSummaryDto>> ListAsync(CancellationToken ct);

    Task<BomDto?> GetDtoAsync(int productId, CancellationToken ct);

    /// <summary>Завантажує специфікацію з рядками для зміни. Зміни зберігаються в <see cref="SaveChangesAsync"/>.</summary>
    Task<Bom?> FindAsync(int productId, CancellationToken ct);

    /// <summary>Повертає ті з <paramref name="stockItemIds"/>, яких немає на складі.</summary>
    Task<IReadOnlyList<int>> MissingStockItemIdsAsync(IReadOnlyCollection<int> stockItemIds, CancellationToken ct);

    /// <summary>Товари замовлення або null, якщо замовлення не існує.</summary>
    Task<IReadOnlyList<OrderProductLine>?> GetOrderProductsAsync(int orderId, CancellationToken ct);

    Task<IReadOnlyList<BomComponent>> GetComponentsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);

    void Add(Bom bom);

    void Remove(Bom bom);

    Task SaveChangesAsync(CancellationToken ct);
}
