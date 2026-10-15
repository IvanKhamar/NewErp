using NewErp.Domain.Production;

namespace NewErp.Application.Production;

public interface IProductionRepository
{
    /// <summary>Активні моделі з підсумками крою й пошиву.</summary>
    Task<IReadOnlyList<ProductionModelSummaryDto>> ListAsync(ProductionModelFilter filter, CancellationToken ct);

    Task<ProductionModelDto?> GetDtoAsync(int productionId, CancellationToken ct);

    /// <summary>Модель з розмірами, прив'язками й специфікацією пошиву для зміни.</summary>
    Task<ProductionModel?> FindModelAsync(int productionId, CancellationToken ct);

    /// <summary>Деталь зі специфікацією крою для зміни.</summary>
    Task<ProductionComponent?> FindComponentAsync(int componentId, CancellationToken ct);

    Task<IReadOnlyList<int>> ComponentIdsAsync(int productionId, CancellationToken ct);

    /// <summary>Повертає ті з <paramref name="itemIds"/>, яких немає серед активних розхідників.</summary>
    Task<IReadOnlyList<int>> MissingStockItemIdsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct);

    /// <summary>Чи посилаються на ці розміри специфікації крою або ненульовий запас деталей.</summary>
    Task<bool> SizesInUseAsync(IReadOnlyCollection<int> sizeIds, CancellationToken ct);

    Task<IReadOnlyList<ComponentStock>> FindComponentStockAsync(int componentId, CancellationToken ct);

    void Add(ProductionModel model);

    void Add(ProductionComponent component);

    void Remove(ProductionComponent component);

    void Remove(ComponentStock stock);

    Task SaveChangesAsync(CancellationToken ct);
}
