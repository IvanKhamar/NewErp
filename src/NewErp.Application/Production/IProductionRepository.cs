using NewErp.Domain.Production;
using NewErp.Domain.Stock;

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

    /// <summary>Деталі моделі зі специфікаціями крою, для операцій.</summary>
    Task<IReadOnlyList<ProductionComponent>> FindComponentsAsync(int productionId, CancellationToken ct);

    /// <summary>Наявні рядки запасу цих деталей (рядка може ще не бути, якщо деталь не кроїли).</summary>
    Task<IReadOnlyList<ComponentStock>> FindComponentStocksAsync(IReadOnlyCollection<int> componentIds, CancellationToken ct);

    /// <summary>Розхідники для зміни залишку.</summary>
    Task<IReadOnlyList<StockItem>> FindStockItemsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct);

    Task<IReadOnlyList<ProductionLogDto>> ListLogAsync(ProductionLogFilter filter, CancellationToken ct);

    /// <summary>Рядки замовлення з моделлю виробництва й опціями (порожньо, якщо замовлення без товарів).</summary>
    Task<IReadOnlyList<OrderSizeLine>> GetOrderSizeLinesAsync(int orderId, CancellationToken ct);

    /// <summary>Моделі з розмірами для зміни залишків.</summary>
    Task<IReadOnlyList<ProductionModel>> FindModelsAsync(IReadOnlyCollection<int> productionIds, CancellationToken ct);

    /// <summary>
    /// Скільки пошитих виробів кожного розміру це замовлення зараз тримає: списане з коментарем «Замовлення #id»
    /// мінус повернене з «Скасування #id». Рахується за спільним журналом, тож враховує й дії старого сайту.
    /// </summary>
    Task<IReadOnlyDictionary<int, decimal>> GetSewnHeldByOrderAsync(int orderId, CancellationToken ct);

    void Add(ProductionLogEntry entry);

    void Add(StockMovement movement);

    void Add(ComponentStock stock);

    void Add(ProductionModel model);

    void Add(ProductionComponent component);

    void Remove(ProductionComponent component);

    void Remove(ComponentStock stock);

    Task SaveChangesAsync(CancellationToken ct);
}
