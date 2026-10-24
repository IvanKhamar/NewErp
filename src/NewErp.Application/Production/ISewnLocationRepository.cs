using NewErp.Domain.Production;

namespace NewErp.Application.Production;

/// <summary>
/// Пошиті вироби на складах (erp_production_location) і переміщення між ними. Зміни зберігає той самий
/// <see cref="IProductionRepository.SaveChangesAsync"/>.
/// </summary>
public interface ISewnLocationRepository
{
    /// <summary>Баланси всіх розмірів моделі для зміни.</summary>
    Task<IReadOnlyList<SewnBalance>> FindBalancesAsync(int productionId, CancellationToken ct);

    /// <summary>Усі ненульові баланси, лише для читання.</summary>
    Task<IReadOnlyList<SewnBalanceRow>> ListBalancesAsync(CancellationToken ct);

    /// <summary>Розміри активних моделей з пошитим залишком, за моделлю й порядком розміру.</summary>
    Task<IReadOnlyList<ActiveSizeRow>> ListActiveSizesAsync(CancellationToken ct);

    Task<IReadOnlyList<SewnTransfer>> ListTransfersAsync(int productionId, CancellationToken ct);

    /// <summary>Чи лежать на складі хоч якісь пошиті вироби.</summary>
    Task<bool> HoldsStockAsync(int warehouseId, CancellationToken ct);

    void Add(SewnBalance balance);

    void Add(SewnTransfer transfer);
}
