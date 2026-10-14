using Microsoft.Extensions.Options;

using NewErp.Domain.Common;
using NewErp.Domain.Staff;

namespace NewErp.Application.Staff;

public sealed class BonusService(IBonusDataSource source, IOptions<BonusOptions> options)
{
    public async Task<BonusReport> CalculateAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from)
            throw new DomainException("Дата кінця періоду раніша за дату початку.");

        var policy = new BonusPolicy(options.Value.Percent, options.Value.CountedStatusIds);

        // Період включає обидві дати, тому верхня межа = наступний день після "to".
        var orders = await source.GetManagerOrdersAsync(
            from.ToDateTime(TimeOnly.MinValue), to.AddDays(1).ToDateTime(TimeOnly.MinValue), ct);

        var bonuses = policy.Calculate(orders);
        var names = await source.GetUserNamesAsync(bonuses.Select(b => b.ManagerId), ct);

        var rows = bonuses
            .Select(b => new ManagerBonusRow(b.ManagerId, names.GetValueOrDefault(b.ManagerId), b.OrdersCount, b.Revenue, b.Bonus))
            .ToList();

        return new BonusReport(from, to, policy.Percent, rows);
    }
}