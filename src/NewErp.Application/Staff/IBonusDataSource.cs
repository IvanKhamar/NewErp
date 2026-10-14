using NewErp.Domain.Staff;

namespace NewErp.Application.Staff;

public interface IBonusDataSource
{
    /// <summary>Замовлення з призначеним менеджером, створені в проміжку [start, endExclusive).</summary>
    Task<IReadOnlyList<BonusOrder>> GetManagerOrdersAsync(DateTime start, DateTime endExclusive, CancellationToken ct);

    Task<IReadOnlyDictionary<int, string>> GetUserNamesAsync(IEnumerable<int> userIds, CancellationToken ct);
}