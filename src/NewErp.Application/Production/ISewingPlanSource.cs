using NewErp.Domain.Production;

namespace NewErp.Application.Production;

/// <summary>Дані для плану пошиву.</summary>
public interface ISewingPlanSource
{
    /// <summary>Замовлення в цих статусах з рядками й тим, що кожне вже тримає за журналом пошиву.</summary>
    Task<IReadOnlyList<PlanOrder>> GetOrdersAsync(IReadOnlyCollection<int> statusIds, CancellationToken ct);

    /// <summary>
    /// Термін виготовлення за id товару: production_period першої категорії товару (з найменшим id), як у старому сайті.
    /// Товарів без категорії у словнику немає.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetProductionDaysAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);

    /// <summary>Моделі з розмірами (лише читання).</summary>
    Task<IReadOnlyList<ProductionModel>> GetModelsAsync(IReadOnlyCollection<int> productionIds, CancellationToken ct);
}
