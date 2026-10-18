namespace NewErp.Application.Catalog;

public interface IPreOrderStore
{
    /// <summary>Розміри товарів за назвою чи артикулом, не більше limit.</summary>
    Task<IReadOnlyList<PreOrderRow>> ListAsync(PreOrderFilter filter, int limit, CancellationToken ct);

    /// <returns>false, якщо розміру немає.</returns>
    Task<bool> UpdateAsync(int productOptionValueId, int quota, DateOnly? dateAvailable, CancellationToken ct);

    /// <summary>Списує продане наперед з лімітів (усіх розмірів або лише цих товарів); повертає кількість змінених розмірів.</summary>
    Task<int> SettleAsync(IReadOnlyCollection<int>? productIds, CancellationToken ct);
}
